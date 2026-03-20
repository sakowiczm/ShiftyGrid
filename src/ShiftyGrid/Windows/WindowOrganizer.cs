using ShiftyGrid.Common;

namespace ShiftyGrid.Windows;

/// <summary>
/// Shared service for applying organize rules to windows.
/// Used by both auto-organize (WindowLifecycleMonitor) and the organize IPC command.
/// </summary>
internal class WindowOrganizer
{
    private readonly WindowMatcher _windowMatcher;
    private readonly int _gap;

    public WindowOrganizer(WindowMatcher windowMatcher, int gap)
    {
        _windowMatcher = windowMatcher ?? throw new ArgumentNullException(nameof(windowMatcher));
        _gap = gap;
    }

    /// <summary>
    /// Tries to organize a single window according to matching rules.
    /// Returns true if the window was positioned successfully.
    /// </summary>
    public bool TryOrganize(Window window)
    {
        if (_windowMatcher.ShouldIgnore(window))
            return false;

        if (window.State == WindowState.Minimized)
            return false;

        if (!window.IsParent)
            return false;

        var matchingRule = _windowMatcher.FindOrganizeRule(window);
        if (matchingRule?.ParsedCoordinates == null)
        {
            Logger.Debug($"WindowOrganizer: No match for '{window.Text}'");
            return false;
        }

        Logger.Info($"WindowOrganizer: Matched '{window.Text}' -> {matchingRule.ParsedCoordinates}");

        try
        {
            return WindowPositioner.ChangePosition(window, matchingRule.ParsedCoordinates.Value, _gap);
        }
        catch (Exception ex)
        {
            Logger.Error($"WindowOrganizer: Error positioning '{window.Text}'", ex);
            return false;
        }
    }

    /// <summary>
    /// Organizes a batch of windows. Returns success/failed/matched counts.
    /// </summary>
    public (int success, int failed, int matched) OrganizeWindows(IEnumerable<Window> windows)
    {
        int success = 0, failed = 0, matched = 0;

        foreach (var window in windows)
        {
            if (_windowMatcher.ShouldIgnore(window))
            {
                Logger.Debug($"WindowOrganizer: Ignoring window '{window.Text}' (matched ignore rule)");
                continue;
            }

            if (window.State == WindowState.Minimized)
                continue;

            if (!window.IsWindowReadyForPositioning())
            {
                Logger.Debug($"WindowOrganizer: Window '{window.Text}' not ready for positioning, skipping");
                continue;
            }

            if (!window.IsParent)
            {
                Logger.Debug($"WindowOrganizer: Skipping child window '{window.Text}'");
                continue;
            }

            var matchedRule = _windowMatcher.FindOrganizeRule(window);
            if (matchedRule == null)
            {
                Logger.Debug($"WindowOrganizer: No match for window '{window.Text}' (class: {window.ClassName})");
                continue;
            }

            matched++;
            Logger.Info($"WindowOrganizer: Matched '{window.Text}' -> {matchedRule.Command}");

            if (matchedRule.ParsedCoordinates == null)
            {
                Logger.Error($"WindowOrganizer: Rule has no parsed position: {matchedRule.Command}");
                failed++;
                continue;
            }

            try
            {
                if (WindowPositioner.ChangePosition(window, matchedRule.ParsedCoordinates.Value, _gap))
                    success++;
                else
                {
                    failed++;
                    Logger.Warning($"WindowOrganizer: Failed to position window '{window.Text}'");
                }
            }
            catch (Exception ex)
            {
                failed++;
                Logger.Error($"WindowOrganizer: Error processing window '{window.Text}': {ex.Message}", ex);
            }
        }

        return (success, failed, matched);
    }
}
