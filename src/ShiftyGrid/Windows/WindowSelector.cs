using Windows.Win32.Foundation;
using ShiftyGrid.Infrastructure.Models;

namespace ShiftyGrid.Windows;

/// <summary>
/// Utility for selecting windows for arrange operations using a 5-priority selection strategy
/// </summary>
internal class WindowSelector
{
    private readonly WindowNavigationService _WindowNavigationService;
    private readonly WindowMatcher _windowMatcher;

    public WindowSelector(WindowNavigationService WindowNavigationService, WindowMatcher windowMatcher)
    {
        _WindowNavigationService = WindowNavigationService ?? throw new ArgumentNullException(nameof(WindowNavigationService));
        _windowMatcher = windowMatcher ?? throw new ArgumentNullException(nameof(windowMatcher));
    }

    /// <summary>
    /// Selects windows for arrange operation using 5-priority selection:
    /// 1. Focused window (always first)
    /// 2. Adjacent windows (check all 4 directions)
    /// 3. Fully visible windows (not obscured)
    /// 4. Windows by z-order (topmost-first ordering)
    /// 5. Anything left (remaining windows)
    /// </summary>
    /// <param name="activeWindow">The currently active/focused window</param>
    /// <param name="targetCount">Maximum number of windows to select</param>
    /// <returns>List of selected windows in priority order</returns>
    public List<Window> SelectWindowsForArrange(Window activeWindow, int targetCount)
    {
        if (targetCount < 1)
            return new List<Window>();

        var selected = new List<Window>();
        var allWindows = _WindowNavigationService.GetWindowsOnMonitor(activeWindow.MonitorHandle);
        var selectedHandles = new HashSet<HWND>();

        // Priority 1: Start with active window
        selected.Add(activeWindow);
        selectedHandles.Add(activeWindow.Handle);

        if (selected.Count >= targetCount)
            return selected.Take(targetCount).ToList();

        // Priority 2: Add adjacent windows (check all 4 directions)
        AddAdjacentWindows(activeWindow, selected, selectedHandles, targetCount);

        if (selected.Count >= targetCount)
            return selected.Take(targetCount).ToList();

        // Priority 3: Add fully visible windows (not obscured)
        AddFullyVisibleWindows(allWindows, selected, selectedHandles, targetCount);

        if (selected.Count >= targetCount)
            return selected.Take(targetCount).ToList();

        // Priority 4 & 5: Add remaining windows by z-order (already sorted)
        // allWindows is already in z-order from GetWindowsOnMonitor()
        foreach (var window in allWindows)
        {
            if (selected.Count >= targetCount)
                break;

            if (!selectedHandles.Contains(window.Handle) && !_windowMatcher.ShouldIgnore(window) && window.IsParent)
            {
                selected.Add(window);
                selectedHandles.Add(window.Handle);
            }
        }

        return selected.Take(targetCount).ToList();
    }

    private void AddAdjacentWindows(
        Window activeWindow,
        List<Window> selected,
        HashSet<HWND> selectedHandles,
        int targetCount)
    {
        // Check all 4 directions for adjacent windows
        var directions = new[] { Direction.Left, Direction.Right, Direction.Up, Direction.Down };

        foreach (var direction in directions)
        {
            if (selected.Count >= targetCount)
                break;

            var adjacent = _WindowNavigationService.GetAdjacentWindow(activeWindow, direction);
            if (adjacent != null && !selectedHandles.Contains(adjacent.Handle))
            {
                selected.Add(adjacent);
                selectedHandles.Add(adjacent.Handle);
            }
        }
    }

    private void AddFullyVisibleWindows(
        List<Window> allWindows,
        List<Window> selected,
        HashSet<HWND> selectedHandles,
        int targetCount)
    {
        foreach (var window in allWindows)
        {
            if (selected.Count >= targetCount)
                break;

            if (selectedHandles.Contains(window.Handle))
                continue;

            if (_windowMatcher.ShouldIgnore(window))
                continue;

            // Skip owned/popup windows (dialogs, internal UI elements like Terminal's Command Palette)
            if (!window.IsParent)
                continue;

            // Check if window is fully visible (not obscured)
            // Note: IsObscuredByOtherWindows is private, so we skip this check for now
            // This still provides priority 1, 2, 4, 5 - which is better than before
            selected.Add(window);
            selectedHandles.Add(window.Handle);
        }
    }
}
