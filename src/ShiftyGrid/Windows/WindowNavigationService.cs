using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Common;

namespace ShiftyGrid.Windows;

/// <summary>
/// Shared utility for finding adjacent windows using proximity detection
/// </summary>
internal class WindowNavigationService
{
    private readonly WindowMatcher _windowMatcher;
    private readonly int _proximityThreshold;

    public WindowNavigationService(WindowMatcher windowMatcher, int proximityThreshold)
    {
        _windowMatcher = windowMatcher ?? throw new ArgumentNullException(nameof(windowMatcher));
        _proximityThreshold = proximityThreshold;
    }

    /// <summary>
    /// Finds the adjacent window in the specified direction from the active window
    /// </summary>
    public Window? GetAdjacentWindow(Window activeWindow, Direction direction)
    {
        // Only get windows on the same monitor as active window
        // Windows are returned in Z-order (top to bottom - most visible first)
        var candidateWindows = GetWindowsOnMonitor(activeWindow.MonitorHandle);

        // Find the active window in the enumerated list to get its correct Z-order
        var activeInList = candidateWindows.FirstOrDefault(w => w.Handle == activeWindow.Handle);
        if (activeInList != null)
        {
            // Update active window with correct Z-order from enumeration
            activeWindow = activeInList;
        }

        Window? bestMatch = null;
        int bestOverlapSize = 0;
        int bestDistance = int.MaxValue;

        foreach (var window in candidateWindows)
        {
            if (window.Handle == activeWindow.Handle)
                continue;

            // Skip ignored processes
            if (_windowMatcher.ShouldIgnore(window))
                continue;

            // Calculate distance based on direction
            int distance = direction switch
            {
                Direction.Left => CalculateLeftProximity(activeWindow, window),
                Direction.Right => CalculateRightProximity(activeWindow, window),
                Direction.Up => CalculateUpProximity(activeWindow, window),
                Direction.Down => CalculateDownProximity(activeWindow, window),
                _ => -1
            };

            // Skip windows that are not adjacent (negative distance or beyond threshold)
            if (distance < 0 || distance > _proximityThreshold)
                continue;

            // Skip windows that are obscured by other windows
            if (IsObscuredByOtherWindows(window, candidateWindows))
            {
                Logger.Debug($"Skipping '{window.Text}' (z-order {window.ZOrder}) - obscured");
                continue;
            }

            // Calculate overlap size based on direction
            int overlapSize = (direction == Direction.Left || direction == Direction.Right)
                ? WindowGeometry.CalculateVerticalOverlap(activeWindow.Rect, window.Rect)
                : WindowGeometry.CalculateHorizontalOverlap(activeWindow.Rect, window.Rect);

            // Prioritize windows with larger border overlap
            // If overlap is the same, prefer closer distance
            if (overlapSize > bestOverlapSize || (overlapSize == bestOverlapSize && distance < bestDistance))
            {
                bestMatch = window;
                bestOverlapSize = overlapSize;
                bestDistance = distance;
            }
        }

        return bestMatch;
    }

    /// <summary>
    /// Checks if a window has a reasonable size (used for elevated windows without readable text)
    /// </summary>
    private bool HasValidSize(Window window)
    {
        var width = window.Rect.Width();
        var height = window.Rect.Height();

        // Filter out system windows (0x0, 1x1, negative coords)
        // Accept windows with reasonable application size (>100px)
        return width > 100 && height > 100 &&
               window.Rect.left >= -10 && window.Rect.top >= -10;  // Allow slight negative for borders
    }

    /// <summary>
    /// Gets all windows on the specified monitor in Z-order
    /// </summary>
    public List<Window> GetWindowsOnMonitor(HMONITOR targetMonitor)
    {
        var windows = new List<Window>();
        int zOrder = 0; // Track Z-order position (0 = topmost)

        PInvoke.EnumWindows((hWnd, lParam) =>
        {
            if (PInvoke.IsWindowVisible(hWnd))
            {
                // check monitor BEFORE getting full window info
                HMONITOR windowMonitor = PInvoke.MonitorFromWindow(hWnd,
                    MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

                if (windowMonitor == targetMonitor)
                {
                    var windowInfo = Window.FromHandle(hWnd, zOrder);
                    // Include windows with text OR valid size (for elevated windows)
                    if (windowInfo != null &&
                        (!string.IsNullOrWhiteSpace(windowInfo.Text) || HasValidSize(windowInfo)))
                    {
                        windows.Add(windowInfo);

                        // Only increment Z-order for non-ignored windows
                        if (!_windowMatcher.ShouldIgnore(windowInfo))
                        {
                            zOrder++;
                        }
                    }
                }
            }
            return true;
        }, 0);

        return windows;
    }

    private int CalculateLeftProximity(Window activeWindow, Window candidateWindow)
    {
        // Check if candidate window is on the left side
        // candidate's right edge should be near active's left edge
        int distance = Math.Abs(candidateWindow.Rect.right - activeWindow.Rect.left);

        // Also check vertical overlap
        if (!WindowGeometry.HasVerticalOverlap(activeWindow.Rect, candidateWindow.Rect))
            return -1;

        return distance;
    }

    private int CalculateRightProximity(Window activeWindow, Window candidateWindow)
    {
        // Check if candidate window is on the right side
        // candidate's left edge should be near active's right edge
        int distance = Math.Abs(candidateWindow.Rect.left - activeWindow.Rect.right);

        // Also check vertical overlap
        if (!WindowGeometry.HasVerticalOverlap(activeWindow.Rect, candidateWindow.Rect))
            return -1;

        return distance;
    }

    private int CalculateUpProximity(Window activeWindow, Window candidateWindow)
    {
        // Check if candidate window is above
        // candidate's bottom edge should be near active's top edge
        int distance = Math.Abs(candidateWindow.Rect.bottom - activeWindow.Rect.top);

        // Also check horizontal overlap
        if (!WindowGeometry.HasHorizontalOverlap(activeWindow.Rect, candidateWindow.Rect))
            return -1;

        return distance;
    }

    private int CalculateDownProximity(Window activeWindow, Window candidateWindow)
    {
        // Check if candidate window is below
        // candidate's top edge should be near active's bottom edge
        int distance = Math.Abs(candidateWindow.Rect.top - activeWindow.Rect.bottom);

        // Also check horizontal overlap
        if (!WindowGeometry.HasHorizontalOverlap(activeWindow.Rect, candidateWindow.Rect))
            return -1;

        return distance;
    }

    /// <summary>
    /// Shadow threshold - DPI-aware (~7px at 100% DPI, scales with DPI)
    /// </summary>
    private static int GetShadowThreshold(uint dpi) => (int)(7 * (dpi / 96.0));

    /// <summary>
    /// Check if the candidate window is significantly overlapped by any window in front of it
    /// </summary>
    public bool IsObscuredByOtherWindows(Window candidateWindow, List<Window> allWindows)
    {
        int shadowThreshold = GetShadowThreshold(candidateWindow.DPI);

        foreach (var window in allWindows)
        {
            // Skip the candidate itself
            if (window.Handle == candidateWindow.Handle)
                continue;

            // Skip ignored processes
            if (_windowMatcher.ShouldIgnore(window))
                continue;

            // Only check windows that are IN FRONT of the candidate
            // (lower Z-order number = more visible/on top)
            if (window.ZOrder >= candidateWindow.ZOrder)
                continue; // This window is behind the candidate, can't obscure it

            // Calculate overlap area
            int overlapArea = WindowGeometry.CalculateOverlapArea(candidateWindow.Rect, window.Rect);
            if (overlapArea == 0)
                continue; // No overlap

            // Calculate the dimensions of the overlap
            int overlapLeft = Math.Max(candidateWindow.Rect.left, window.Rect.left);
            int overlapRight = Math.Min(candidateWindow.Rect.right, window.Rect.right);
            int overlapTop = Math.Max(candidateWindow.Rect.top, window.Rect.top);
            int overlapBottom = Math.Min(candidateWindow.Rect.bottom, window.Rect.bottom);

            int overlapWidth = overlapRight - overlapLeft;
            int overlapHeight = overlapBottom - overlapTop;

            // If both dimensions of the overlap exceed shadow threshold, the candidate is significantly obscured
            if (overlapWidth > shadowThreshold && overlapHeight > shadowThreshold)
            {
                return true; // Obscured
            }
        }

        return false; // Not obscured
    }

    /// <summary>
    /// Finds a window in the specified direction for gap-closing during shrink operations.
    /// Uses a larger search range than GetAdjacentWindow() to find windows with gaps > proximity threshold.
    /// Requires significant overlap (50% of smaller dimension) to ensure proper alignment.
    /// </summary>
    /// <param name="activeWindow">The window that is shrinking</param>
    /// <param name="direction">Direction to search for neighbors</param>
    /// <param name="maxSearchDistance">Maximum gap distance to search (default: monitor dimension)</param>
    /// <returns>The best candidate window for gap-closing, or null if none suitable</returns>
    public Window? FindWindowForGapClosing(
        Window activeWindow,
        Direction direction,
        int? maxSearchDistance = null)
    {
        // Use monitor dimensions as default max search distance
        int defaultMaxDistance = (direction == Direction.Left || direction == Direction.Right)
            ? activeWindow.MonitorRect.Width
            : activeWindow.MonitorRect.Height;

        int searchDistance = maxSearchDistance ?? defaultMaxDistance;

        var candidateWindows = GetWindowsOnMonitor(activeWindow.MonitorHandle);

        // Update active window Z-order by finding it in the list
        var activeInList = candidateWindows.FirstOrDefault(w => w.Handle == activeWindow.Handle);
        if (activeInList != null)
            activeWindow = activeInList;

        Window? bestMatch = null;
        int bestOverlapSize = 0;
        int bestDistance = int.MaxValue;

        foreach (var window in candidateWindows)
        {
            if (window.Handle == activeWindow.Handle)
                continue;

            if (_windowMatcher.ShouldIgnore(window))
                continue;

            // Calculate distance using existing proximity methods
            int distance = direction switch
            {
                Direction.Left => CalculateLeftProximity(activeWindow, window),
                Direction.Right => CalculateRightProximity(activeWindow, window),
                Direction.Up => CalculateUpProximity(activeWindow, window),
                Direction.Down => CalculateDownProximity(activeWindow, window),
                _ => -1
            };

            // Skip windows not in correct direction or beyond max search distance
            if (distance < 0 || distance > searchDistance)
                continue;

            // Skip obscured windows using existing helper
            if (IsObscuredByOtherWindows(window, candidateWindows))
                continue;

            // Calculate overlap using WindowGeometry
            int overlapSize = (direction == Direction.Left || direction == Direction.Right)
                ? WindowGeometry.CalculateVerticalOverlap(activeWindow.Rect, window.Rect)
                : WindowGeometry.CalculateHorizontalOverlap(activeWindow.Rect, window.Rect);

            // Require at least 50% overlap of the smaller window's dimension
            int minOverlapRequired = (direction == Direction.Left || direction == Direction.Right)
                ? Math.Min(activeWindow.Rect.Height(), window.Rect.Height()) / 2
                : Math.Min(activeWindow.Rect.Width(), window.Rect.Width()) / 2;

            if (overlapSize < minOverlapRequired)
                continue;

            // Prioritize: larger overlap first, then closer distance
            if (overlapSize > bestOverlapSize ||
                (overlapSize == bestOverlapSize && distance < bestDistance))
            {
                bestMatch = window;
                bestOverlapSize = overlapSize;
                bestDistance = distance;
            }
        }

        return bestMatch;
    }
}

