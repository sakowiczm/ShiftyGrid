using ShiftyGrid.Common;
using ShiftyGrid.Infrastructure.Models;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ShiftyGrid.Configuration;

/// <summary>
/// Custom exception for configuration errors
/// </summary>
public class ConfigurationException : Exception
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Service for loading, validating, and saving YAML configuration
/// </summary>
public static class ConfigurationService
{
    private const string DefaultConfigFileName = "config.yaml";

    /// <summary>
    /// Loads and validates configuration from file
    /// </summary>
    public static ShiftyGridConfig LoadConfiguration(string? configPath)
    {
        var resolvedPath = ResolveConfigPath(configPath);

        if (!File.Exists(resolvedPath))
        {
            Logger.Info($"Config file not found at {resolvedPath}, generating default configuration");
            var defaultConfig = CreateDefaultConfig();
            SaveConfiguration(resolvedPath, defaultConfig);
            return defaultConfig;
        }

        try
        {
            var yaml = File.ReadAllText(resolvedPath);
            var context = new YamlStaticContext();
            var deserializer = new StaticDeserializerBuilder(context)
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var config = deserializer.Deserialize<ShiftyGridConfig>(yaml);
            ValidateAndProcessConfig(config);
            Logger.Info($"Configuration loaded from {resolvedPath}");
            return config;
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ConfigurationException($"Invalid YAML syntax in config file: {ex.Message}", ex);
        }
        catch (ConfigurationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ConfigurationException($"Failed to load configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Resolves config path: CLI option > exe directory
    /// </summary>
    public static string ResolveConfigPath(string? providedPath)
    {
        if (!string.IsNullOrWhiteSpace(providedPath))
        {
            // Use provided path (can be relative or absolute)
            return Path.IsPathRooted(providedPath)
                ? providedPath
                : Path.GetFullPath(providedPath);
        }

        // Default to exe directory
        var exeDirectory = AppContext.BaseDirectory;
        return Path.Combine(exeDirectory, DefaultConfigFileName);
    }

    /// <summary>
    /// Validates configuration and throws ConfigurationException if invalid
    /// </summary>
    public static void ValidateAndProcessConfig(ShiftyGridConfig config)
    {
        // Validate at least one match field in each rule
        foreach (var rule in config.Organize.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Match.TitlePattern) &&
                string.IsNullOrWhiteSpace(rule.Match.ClassName) &&
                string.IsNullOrWhiteSpace(rule.Match.ProcessName))
            {
                throw new ConfigurationException("Organize rule must have at least one match field (title_pattern, class_name, or process_name)");
            }

            if (string.IsNullOrWhiteSpace(rule.Command))
            {
                throw new ConfigurationException("Organize rule must have a command");
            }
        }

        foreach (var rule in config.Ignore.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Match.TitlePattern) &&
                string.IsNullOrWhiteSpace(rule.Match.ClassName) &&
                string.IsNullOrWhiteSpace(rule.Match.ProcessName))
            {
                throw new ConfigurationException("Ignore rule must have at least one match field (title_pattern, class_name, or process_name)");
            }
        }

        // Validate no overlap between organize and ignore rules
        foreach (var organizeRule in config.Organize.Rules)
        {
            foreach (var ignoreRule in config.Ignore.Rules)
            {
                if (MatchConfigsOverlap(organizeRule.Match, ignoreRule.Match))
                {
                    throw new ConfigurationException(
                        $"Window cannot appear in both organize and ignore rules. " +
                        $"Overlapping patterns detected.");
                }
            }
        }

        // Validate regex patterns compile successfully
        ValidateRegexPatterns(config.Organize.Rules.Select(r => r.Match));
        ValidateRegexPatterns(config.Ignore.Rules.Select(r => r.Match));

        // Validate duplicate shortcuts
        ValidateDuplicateShortcuts(config.Keyboard);

        // Validate command syntax (basic check)
        foreach (var shortcut in config.Keyboard.Shortcuts)
        {
            if (string.IsNullOrWhiteSpace(shortcut.Command))
            {
                throw new ConfigurationException($"Shortcut with bindings [{string.Join(", ", shortcut.Bindings)}] has empty command");
            }
        }

        foreach (var mode in config.Keyboard.Modes)
        {
            foreach (var shortcut in mode.Shortcuts)
            {
                if (string.IsNullOrWhiteSpace(shortcut.Command))
                {
                    throw new ConfigurationException($"Mode '{mode.Id}' shortcut with bindings [{string.Join(", ", shortcut.Bindings)}] has empty command");
                }
            }
        }

        ValidateStartupCommands(config);
        ParseOrganizeCommands(config);
    }

    private static bool MatchConfigsOverlap(WindowMatchConfig match1, WindowMatchConfig match2)
    {
        // Check if patterns are identical (case-insensitive)
        bool titleMatch = !string.IsNullOrWhiteSpace(match1.TitlePattern) &&
                         !string.IsNullOrWhiteSpace(match2.TitlePattern) &&
                         string.Equals(match1.TitlePattern, match2.TitlePattern, StringComparison.OrdinalIgnoreCase);

        bool classMatch = !string.IsNullOrWhiteSpace(match1.ClassName) &&
                         !string.IsNullOrWhiteSpace(match2.ClassName) &&
                         string.Equals(match1.ClassName, match2.ClassName, StringComparison.OrdinalIgnoreCase);

        bool processMatch = !string.IsNullOrWhiteSpace(match1.ProcessName) &&
                           !string.IsNullOrWhiteSpace(match2.ProcessName) &&
                           string.Equals(match1.ProcessName, match2.ProcessName, StringComparison.OrdinalIgnoreCase);

        // If any field matches, consider it an overlap
        return titleMatch || classMatch || processMatch;
    }

    private static void ValidateRegexPatterns(IEnumerable<WindowMatchConfig> matches)
    {
        foreach (var match in matches)
        {
            ValidateRegexPattern(match.TitlePattern, "title_pattern");
            ValidateRegexPattern(match.ClassName, "class_name");
            ValidateRegexPattern(match.ProcessName, "process_name");
        }
    }

    private static void ValidateRegexPattern(string? pattern, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return;

        if (pattern.StartsWith("regex:", StringComparison.OrdinalIgnoreCase))
        {
            var regexPattern = pattern.Substring("regex:".Length);
            try
            {
                _ = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch (ArgumentException ex)
            {
                throw new ConfigurationException($"Invalid regex pattern in {fieldName}: '{regexPattern}' - {ex.Message}", ex);
            }
        }
    }

    private static void ValidateStartupCommands(ShiftyGridConfig config)
    {
        var disallowedCommands = new[] { "exit", "start", "reload" };

        foreach (var commandString in config.Startup.Commands)
        {
            if (string.IsNullOrWhiteSpace(commandString))
            {
                throw new ConfigurationException("Startup command cannot be empty");
            }

            var commandName = commandString.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (commandName != null && disallowedCommands.Contains(commandName.ToLowerInvariant()))
            {
                throw new ConfigurationException(
                    $"Command '{commandName}' is not allowed in startup commands");
            }
        }

        if (config.Startup.Commands.Count > 0)
        {
            Logger.Info($"Configured {config.Startup.Commands.Count} startup command(s)");
        }
    }

    /// <summary>
    /// Parses organize rule commands at configuration load time
    /// </summary>
    private static void ParseOrganizeCommands(ShiftyGridConfig config)
    {
        var configGridDefault = config.General.Grid ?? "12x12";

        foreach (var rule in config.Organize.Rules)
        {
            try
            {
                // Parse command string to extract Coordinates
                // Expected format: "move --grid 12x12 --coordinates 0,0,6,12" or "move --coordinates 0,0,6,12"
                var parts = rule.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0 || parts[0].ToLowerInvariant() != "move")
                {
                    throw new ConfigurationException($"Organize rule command must be a 'move' command, got: {rule.Command}");
                }

                string? gridString = null;
                string? coordinatesString = null;

                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i] == "--grid" && i + 1 < parts.Length)
                    {
                        gridString = parts[++i];
                    }
                    else if (parts[i] == "--coordinates" && i + 1 < parts.Length)
                    {
                        coordinatesString = parts[++i];
                    }
                }

                if (string.IsNullOrWhiteSpace(coordinatesString))
                {
                    throw new ConfigurationException($"Organize rule command missing --coordinates argument: {rule.Command}");
                }

                // Use CoordinatesParser to parse coordinates
                var coordinates = Coordinates.Parse(coordinatesString, gridString ?? configGridDefault);
                rule.ParsedCoordinates = coordinates;

                Logger.Debug($"Parsed organize rule: {rule.Command} -> Coordinates({coordinates.StartX},{coordinates.StartY},{coordinates.EndX},{coordinates.EndY})");
            }
            catch (ArgumentException ex)
            {
                throw new ConfigurationException($"Invalid organize rule command '{rule.Command}': {ex.Message}", ex);
            }
        }
    }

    private static void ValidateDuplicateShortcuts(KeyboardSettings keyboard)
    {
        var allBindings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Check global shortcuts
        foreach (var shortcut in keyboard.Shortcuts)
        {
            foreach (var binding in shortcut.Bindings)
            {
                if (!allBindings.Add(binding))
                {
                    Logger.Warning($"Duplicate shortcut binding detected: {binding}");
                }
            }
        }

        // Check mode activation bindings
        foreach (var mode in keyboard.Modes)
        {
            foreach (var binding in mode.Activation)
            {
                if (!allBindings.Add(binding))
                {
                    Logger.Warning($"Duplicate shortcut binding detected (mode activation): {binding}");
                }
            }
        }

        // Mode shortcuts are scoped to the mode, so duplicates within different modes are allowed
        // But we can warn about duplicates within the same mode
        foreach (var mode in keyboard.Modes)
        {
            var modeBindings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var shortcut in mode.Shortcuts)
            {
                foreach (var binding in shortcut.Bindings)
                {
                    if (!modeBindings.Add(binding))
                    {
                        Logger.Warning($"Duplicate shortcut binding in mode '{mode.Id}': {binding}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Saves configuration to YAML file
    /// </summary>
    public static void SaveConfiguration(string path, ShiftyGridConfig config)
    {
        try
        {
            var context = new YamlStaticContext();
            var serializer = new StaticSerializerBuilder(context)
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(YamlDotNet.Serialization.DefaultValuesHandling.OmitDefaults)
                .Build();

            var yaml = serializer.Serialize(config);
            File.WriteAllText(path, yaml);
            Logger.Info($"Configuration saved to {path}");
        }
        catch (Exception ex)
        {
            throw new ConfigurationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates default configuration with all current hardcoded shortcuts
    /// </summary>
    public static ShiftyGridConfig CreateDefaultConfig()
    {
        var config = new ShiftyGridConfig
        {
            General = new GeneralSettings
            {
                Gap = 4,
                Grid = "12x12",
                ProximityThreshold = 20,
                LogLevel = "info"
            },
            Startup = new StartupSettings
            {
                Commands = new List<string> { "organize --all" }
            },
            Keyboard = new KeyboardSettings
            {
                Shortcuts = new List<ShortcutConfig>
                {
                    // Swap shortcuts
                    new() { Bindings = new List<string> { "ctrl+alt+left" }, Command = "swap left" },
                    new() { Bindings = new List<string> { "ctrl+alt+right" }, Command = "swap right" },
                    new() { Bindings = new List<string> { "ctrl+alt+up" }, Command = "swap up" },
                    new() { Bindings = new List<string> { "ctrl+alt+down" }, Command = "swap down" },

                    // Focus navigation
                    new() { Bindings = new List<string> { "ctrl+win+left" }, Command = "focus left" },
                    new() { Bindings = new List<string> { "ctrl+win+right" }, Command = "focus right" },
                    new() { Bindings = new List<string> { "ctrl+win+up" }, Command = "focus up" },
                    new() { Bindings = new List<string> { "ctrl+win+down" }, Command = "focus down" },

                    // Arrange shortcuts
                    new() { Bindings = new List<string> { "ctrl+alt+=", "ctrl+alt+plus" }, Command = "arrange --rows 1 --cols 2" },
                    new() { Bindings = new List<string> { "ctrl+alt+3" }, Command = "arrange --rows 1 --cols 3" },
                    new() { Bindings = new List<string> { "ctrl+alt+4" }, Command = "arrange --rows 2 --cols 2" },

                    // Promote and organize
                    new() { Bindings = new List<string> { "ctrl+alt+return" }, Command = "promote --coordinates 1,0,11,12 --grid 12x12" },
                    new() { Bindings = new List<string> { "ctrl+shift+o" }, Command = "organize" }
                },
                Modes = new List<ModeConfig>
                {
                    new ModeConfig
                    {
                        Id = "move_mode",
                        Name = "Move Mode",
                        Activation = new List<string> { "ctrl+shift+d" },
                        AllowEscape = true,
                        Shortcuts = new List<ModeShortcutConfig>
                        {
                            new() { Bindings = new List<string> { "1" }, Command = "move --coordinates 0,0,6,12" },
                            new() { Bindings = new List<string> { "2" }, Command = "move --coordinates 6,0,12,12" },
                            new() { Bindings = new List<string> { "s" }, Command = "move --coordinates 2,0,10,12" },
                            new() { Bindings = new List<string> { "space" }, Command = "move --coordinates 1,0,11,12" },
                            new() { Bindings = new List<string> { "f" }, Command = "move --coordinates 0,0,12,12" },
                            new() { Bindings = new List<string> { ",", "comma" }, Command = "move --coordinates 0,0,4,12" },
                            new() { Bindings = new List<string> { ".", "period" }, Command = "move --coordinates 4,0,8,12" },
                            new() { Bindings = new List<string> { "/", "slash" }, Command = "move --coordinates 8,0,12,12" },
                            new() { Bindings = new List<string> { "o" }, Command = "move --coordinates 0,0,6,6" },
                            new() { Bindings = new List<string> { "p" }, Command = "move --coordinates 6,0,12,6" },
                            new() { Bindings = new List<string> { "k" }, Command = "move --coordinates 0,6,6,12" },
                            new() { Bindings = new List<string> { "l" }, Command = "move --coordinates 6,6,12,12" }
                        }
                    },
                    new ModeConfig
                    {
                        Id = "resize_mode",
                        Name = "Resize Mode",
                        Activation = new List<string> { "ctrl+shift+r" },
                        AllowEscape = true,
                        Shortcuts = new List<ModeShortcutConfig>
                        {
                            new() { Bindings = new List<string> { "left" }, Command = "resize left", ExitMode = false },
                            new() { Bindings = new List<string> { "right" }, Command = "resize right", ExitMode = false },
                            new() { Bindings = new List<string> { "up" }, Command = "resize up", ExitMode = false },
                            new() { Bindings = new List<string> { "down" }, Command = "resize down", ExitMode = false },
                            new() { Bindings = new List<string> { "shift+left" }, Command = "resize left --outer", ExitMode = false },
                            new() { Bindings = new List<string> { "shift+right" }, Command = "resize right --outer", ExitMode = false },
                            new() { Bindings = new List<string> { "shift+up" }, Command = "resize up --outer", ExitMode = false },
                            new() { Bindings = new List<string> { "shift+down" }, Command = "resize down --outer", ExitMode = false }
                        }
                    }
                }
            },
            Organize = new OrganizeSettings
            {
                Rules = new List<OrganizeRule>
                {
                    new() { Match = new WindowMatchConfig { ProcessName = "WindowsTerminal" }, Command = "move --coordinates 0,0,6,12" },
                    new() { Match = new WindowMatchConfig { TitlePattern = "Slack" }, Command = "move --coordinates 6,0,12,12" },
                    new() { Match = new WindowMatchConfig { TitlePattern = "Docker Desktop" }, Command = "move --coordinates 6,0,12,12" },
                    new() { Match = new WindowMatchConfig { ProcessName = "Fork" }, Command = "move --coordinates 0,0,6,12" },
                    new() { Match = new WindowMatchConfig { ProcessName = "Code" }, Command = "move --coordinates 0,0,6,12" }
                }
            },
            Ignore = new IgnoreSettings
            {
                Rules = new List<IgnoreRule>
                {
                    new() { Match = new WindowMatchConfig { ClassName = "ClunkyBordersOverlayClass" } }
                }
            }
        };

        return config;
    }
}
