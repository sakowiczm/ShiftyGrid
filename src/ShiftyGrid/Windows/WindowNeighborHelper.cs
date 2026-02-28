using ShiftyGrid.Common;
using ShiftyGrid.Handlers;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace ShiftyGrid.Windows;

// todo: rename class?

/// <summary>
/// Shared utility for finding adjacent windows using proximity detection
/// </summary>
internal static class WindowNeighborHelper
{
    // todo: move to configuration

    public const int PROXIMITY_THRESHOLD = 20;   
    private const string IGNORED_WINDOW_CLASS = "ClunkyBordersOverlayClass";

    // todo: move to window either?

    /// <summary>
    /// Finds the adjacent window in the specified direction from the active window
    /// </summary>
    public static Window? GetAdjacentWindow(Window activeWindow, Direction direction)
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
            if (window.ClassName == IGNORED_WINDOW_CLASS)
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
            if (distance < 0 || distance > PROXIMITY_THRESHOLD)
                continue;

            // Skip windows that are obscured by other windows
            if (IsObscuredByOtherWindows(window, candidateWindows))
            {
                Logger.Debug($"Skipping '{window.Text}' (z-order {window.ZOrder}) - obscured");
                continue;
            }

            // Calculate overlap size based on direction
            // Horizontal swapping (Left/Right) uses vertical overlap
            // Vertical swapping (Up/Down) uses horizontal overlap
            int overlapSize = (direction == Direction.Left || direction == Direction.Right)
                ? CalculateVerticalOverlap(activeWindow.Rect, window.Rect)
                : CalculateHorizontalOverlap(activeWindow.Rect, window.Rect);

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

    // todo: move to Window?

    /// <summary>
    /// Gets all windows on the specified monitor in Z-order
    /// </summary>
    public static List<Window> GetWindowsOnMonitor(HMONITOR targetMonitor)
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
                    if (windowInfo != null && !string.IsNullOrWhiteSpace(windowInfo.Text))
                    {
                        windows.Add(windowInfo);

                        // Only increment Z-order for non-ignored windows
                        if (windowInfo.ClassName != IGNORED_WINDOW_CLASS)
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

    private static int CalculateLeftProximity(Window activeWindow, Window candidateWindow)
    {
        // Check if candidate window is on the left side
        // candidate's right edge should be near active's left edge
        int distance = Math.Abs(candidateWindow.Rect.right - activeWindow.Rect.left);

        // Also check vertical overlap
        if (!HasVerticalOverlap(activeWindow.Rect, candidateWindow.Rect))
            return -1;

        return distance;
    }

    private static int CalculateRightProximity(Window activeWindow, Window candidateWindow)
    {
        // Check if candidate window is on the right side
        // candidate's left edge should be near active's right edge
        int distance = Math.Abs(candidateWindow.Rect.left - activeWindow.Rect.right);

        // Also check vertical overlap
        if (!HasVerticalOverlap(activeWindow.Rect, candidateWindow.Rect))
            return -1;

        return distance;
    }

    private static int CalculateUpProximity(Window activeWindow, Window candidateWindow)
    {
        // Check if candidate window is above
        // candidate's bottom edge should be near active's top edge
        int distance = Math.Abs(candidateWindow.Rect.bottom - activeWindow.Rect.top);

        // Also check horizontal overlap
        if (!HasHorizontalOverlap(activeWindow.Rect, candidateWindow.Rect))
            return -1;

        return distance;
    }

    private static int CalculateDownProximity(Window activeWindow, Window candidateWindow)
    {
        // Check if candidate window is below
        // candidate's top edge should be near active's bottom edge
        int distance = Math.Abs(candidateWindow.Rect.top - activeWindow.Rect.bottom);

        // Also check horizontal overlap
        if (!HasHorizontalOverlap(activeWindow.Rect, candidateWindow.Rect))
            return -1;

        return distance;
    }

    private static bool HasVerticalOverlap(RECT rect1, RECT rect2)
    {
        // Check if there's any vertical overlap between two windows
        return rect1.top < rect2.bottom && rect1.bottom > rect2.top;
    }

    private static bool HasHorizontalOverlap(RECT rect1, RECT rect2)
    {
        // Check if there's any horizontal overlap between two windows
        return rect1.left < rect2.right && rect1.right > rect2.left;
    }

    private static int CalculateVerticalOverlap(RECT rect1, RECT rect2)
    {
        // Calculate the size of vertical overlap between two windows
        int overlapTop = Math.Max(rect1.top, rect2.top);
        int overlapBottom = Math.Min(rect1.bottom, rect2.bottom);

        if (overlapTop >= overlapBottom)
            return 0; // No overlap

        return overlapBottom - overlapTop;
    }

    private static int CalculateHorizontalOverlap(RECT rect1, RECT rect2)
    {
        // Calculate the size of horizontal overlap between two windows
        int overlapLeft = Math.Max(rect1.left, rect2.left);
        int overlapRight = Math.Min(rect1.right, rect2.right);

        if (overlapLeft >= overlapRight)
            return 0; // No overlap

        return overlapRight - overlapLeft;
    }

    private static int CalculateRectOverlapArea(RECT rect1, RECT rect2)
    {
        // Calculate the area of overlap between two rectangles
        int overlapLeft = Math.Max(rect1.left, rect2.left);
        int overlapRight = Math.Min(rect1.right, rect2.right);
        int overlapTop = Math.Max(rect1.top, rect2.top);
        int overlapBottom = Math.Min(rect1.bottom, rect2.bottom);

        if (overlapLeft >= overlapRight || overlapTop >= overlapBottom)
            return 0; // No overlap

        return (overlapRight - overlapLeft) * (overlapBottom - overlapTop);
    }

    /// <summary>
    /// Shadow threshold - DPI-aware (~7px at 100% DPI, scales with DPI)
    /// </summary>
    private static int GetShadowThreshold(uint dpi) => (int)(7 * (dpi / 96.0));

    /// <summary>
    /// Check if the candidate window is significantly overlapped by any window in front of it
    /// </summary>
    private static bool IsObscuredByOtherWindows(Window candidateWindow, List<Window> allWindows)
    {
        int shadowThreshold = GetShadowThreshold(candidateWindow.DPI);

        foreach (var window in allWindows)
        {
            // Skip the candidate itself
            if (window.Handle == candidateWindow.Handle)
                continue;

            // Skip ignored processes
            if (window.ClassName == IGNORED_WINDOW_CLASS)
                continue;

            // Only check windows that are IN FRONT of the candidate
            // (lower Z-order number = more visible/on top)
            if (window.ZOrder >= candidateWindow.ZOrder)
                continue; // This window is behind the candidate, can't obscure it

            // Calculate overlap area
            int overlapArea = CalculateRectOverlapArea(candidateWindow.Rect, window.Rect);
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
    /// Uses a larger search range than GetAdjacentWindow() to find windows with gaps > PROXIMITY_THRESHOLD.
    /// Requires significant overlap (50% of smaller dimension) to ensure proper alignment.
    /// </summary>
    /// <param name="activeWindow">The window that is shrinking</param>
    /// <param name="direction">Direction to search for neighbors</param>
    /// <param name="maxSearchDistance">Maximum gap distance to search (default: monitor dimension)</param>
    /// <returns>The best candidate window for gap-closing, or null if none suitable</returns>
    public static Window? FindWindowForGapClosing(
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

            if (window.ClassName == IGNORED_WINDOW_CLASS)
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

            // Calculate overlap using existing methods
            int overlapSize = (direction == Direction.Left || direction == Direction.Right)
                ? CalculateVerticalOverlap(activeWindow.Rect, window.Rect)
                : CalculateHorizontalOverlap(activeWindow.Rect, window.Rect);

            // Require at least 50% overlap of the smaller window's dimension
            int minOverlapRequired = (direction == Direction.Left || direction == Direction.Right)
                ? Math.Min(activeWindow.Rect.bottom - activeWindow.Rect.top,
                           window.Rect.bottom - window.Rect.top) / 2
                : Math.Min(activeWindow.Rect.right - activeWindow.Rect.left,
                           window.Rect.right - window.Rect.left) / 2;

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

