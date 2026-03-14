using System.Text.RegularExpressions;
using ShiftyGrid.Windows;

namespace ShiftyGrid.Configuration;

/// <summary>
/// Unified window matching functionality for organizing and ignoring windows.
/// Consolidates pattern matching logic from WindowMatchConfig and WindowIgnoreChecker.
/// </summary>
internal class WindowMatcher
{
    private readonly ShiftyGridConfig _config;

    // Static regex cache for performance (shared across all instances)
    private static readonly Dictionary<string, Regex> _regexCache = new();
    private static readonly object _regexCacheLock = new();

    public WindowMatcher(ShiftyGridConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Checks if window matches a specific match configuration rule.
    /// Uses AND logic: all specified fields must match.
    /// </summary>
    /// <param name="window">The window to check</param>
    /// <param name="matchConfig">The match configuration containing patterns</param>
    /// <returns>True if window matches all specified patterns in the config</returns>
    public bool Matches(Window window, WindowMatchConfig matchConfig)
    {
        bool titleMatch = MatchesPattern(window.Text, matchConfig.TitlePattern);
        bool classMatch = MatchesPattern(window.ClassName, matchConfig.ClassName);
        bool processMatch = MatchesPattern(window.GetProcessName(), matchConfig.ProcessName);

        // AND logic: all specified fields must match
        bool titleRequired = !string.IsNullOrWhiteSpace(matchConfig.TitlePattern);
        bool classRequired = !string.IsNullOrWhiteSpace(matchConfig.ClassName);
        bool processRequired = !string.IsNullOrWhiteSpace(matchConfig.ProcessName);

        if (titleRequired && !titleMatch) return false;
        if (classRequired && !classMatch) return false;
        if (processRequired && !processMatch) return false;

        // At least one field must be specified and matched
        return titleRequired || classRequired || processRequired;
    }

    /// <summary>
    /// Checks if window should be ignored globally based on ignore rules.
    /// Uses OR logic: if ANY ignore rule matches, window should be ignored.
    /// </summary>
    /// <param name="window">The window to check</param>
    /// <returns>True if window matches any ignore rule</returns>
    public bool ShouldIgnore(Window window)
    {
        // OR logic: if ANY rule matches, window is ignored
        foreach (var rule in _config.Ignore.Rules)
        {
            if (Matches(window, rule.Match))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the first organize rule that matches the window.
    /// Uses first-match logic: returns the first matching rule.
    /// </summary>
    /// <param name="window">The window to check</param>
    /// <returns>The first matching OrganizeRule, or null if no rule matches</returns>
    public OrganizeRule? FindOrganizeRule(Window window)
    {
        foreach (var rule in _config.Organize.Rules)
        {
            if (Matches(window, rule.Match))
            {
                return rule;
            }
        }

        return null;
    }

    /// <summary>
    /// Matches a window field against a pattern (exact or regex).
    /// Supports "regex:" prefix for regex patterns, otherwise uses case-insensitive substring match.
    /// </summary>
    private static bool MatchesPattern(string? windowValue, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true; // Not specified, always matches
        }

        if (string.IsNullOrWhiteSpace(windowValue))
        {
            return false; // Window value is empty but pattern is specified
        }

        // Check if pattern uses regex prefix
        if (pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            var regexPattern = pattern.Substring("regex:".Length);
            return MatchesRegex(windowValue, regexPattern);
        }

        // Default: exact substring match (case-insensitive)
        return windowValue.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Matches using regex pattern with caching for performance.
    /// Thread-safe regex compilation and caching.
    /// </summary>
    private static bool MatchesRegex(string value, string pattern)
    {
        Regex? regex;

        lock (_regexCacheLock)
        {
            if (!_regexCache.TryGetValue(pattern, out regex))
            {
                try
                {
                    regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    _regexCache[pattern] = regex;
                }
                catch (ArgumentException ex)
                {
                    throw new ConfigurationException($"Invalid regex pattern: {pattern} - {ex.Message}", ex);
                }
            }
        }

        return regex.IsMatch(value);
    }
}
