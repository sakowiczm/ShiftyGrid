using ShiftyGrid.Common;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ShiftyGrid.Handlers;

// todo: extrat

internal enum Direction
{
    Left,
    Right,
    Up,
    Down
}

internal class SwapCommandHandler : RequestHandler<Direction>
{

    // todo:
    //  - consider different name
    //  
    //  When we have two adjecent windows (within x pixels, not overlaping) we can swap it's positions
    //  no matter if this is horizontal or vertical swap.

    // Swap Left    Ctrl + Alt + Left Arrow
    // Swap Right   Ctrl + Alt + Right Arrow
    // Swap Up    Ctrl + Alt + Up
    // Swap Down   Ctrl + Alt + Down

    protected override Response Handle(Direction direction)
    {
        try
        {
            var success = Execute(direction);
            return success
                ? Response.CreateSuccess("Windows swapped")
                : Response.CreateError("Error swapping windows");
        }
        catch (Exception ex)
        {
            Logger.Error("Exception swapping windows", ex);
            return Response.CreateError("Error swapping windows");
        }
    }

    protected override JsonTypeInfo<Direction> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.Direction;
    }

    // todo: add to configuration

    //private const int PROXIMITY_THRESHOLD = 20;

    public bool Execute(Direction direction)
    {
        // todo: unify logging

        var activeWindow = Window.GetForeground();
        if (activeWindow == null)
            return false;

        // todo: if active window is maximized or IsFullscreen or is one of the excluded windows or is not visible etc. abandon

        var adjacentWindow = WindowNeighborHelper.GetAdjacentWindow(activeWindow, direction);
        if (adjacentWindow == null)
            return false;

        return SwapWindows(activeWindow, adjacentWindow);
    }

    //public static Window? GetAdjacentWindow(Window activeWindow, Direction direction)
    //{
    //    // Only get windows on the same monitor as active window
    //    // Windows are returned in Z-order (top to bottom - most visible first)
    //    var candidateWindows = GetWindowsOnMonitor(activeWindow.MonitorHandle);

    //    // Find the active window in the enumerated list to get its correct Z-order
    //    var activeInList = candidateWindows.FirstOrDefault(w => w.Handle == activeWindow.Handle);
    //    if (activeInList != null)
    //    {
    //        // Update active window with correct Z-order from enumeration
    //        activeWindow = activeInList;
    //    }

    //    Window? bestMatch = null;
    //    int bestOverlapSize = 0;
    //    int bestDistance = int.MaxValue;

    //    foreach (var window in candidateWindows)
    //    {
    //        if (window.Handle == activeWindow.Handle)
    //            continue;

    //        // Skip ignored processes
    //        // todo: verify, introduce more elaborate filtering?
    //        if (window.ClassName == "ClunkyBordersOverlayClass")
    //            continue;

    //        // Calculate distance based on direction
    //        int distance = direction switch
    //        {
    //            Direction.Left => CalculateLeftProximity(activeWindow, window),
    //            Direction.Right => CalculateRightProximity(activeWindow, window),
    //            Direction.Up => CalculateUpProximity(activeWindow, window),
    //            Direction.Down => CalculateDownProximity(activeWindow, window),
    //            _ => -1
    //        };

    //        // Skip windows that are not adjacent (negative distance or beyond threshold)
    //        if (distance < 0 || distance > PROXIMITY_THRESHOLD)
    //            continue;

    //        // Skip windows that are obscured by other windows
    //        if (IsObscuredByOtherWindows(window, candidateWindows))
    //        {
    //            Logger.Debug($"Swap → Skipping '{window.Text}' (z-order {window.ZOrder}) - obscured");
    //            continue;
    //        }

    //        // Calculate overlap size based on direction
    //        // Horizontal swapping (Left/Right) uses vertical overlap
    //        // Vertical swapping (Up/Down) uses horizontal overlap
    //        int overlapSize = (direction == Direction.Left || direction == Direction.Right)
    //            ? CalculateVerticalOverlap(activeWindow.Rect, window.Rect)
    //            : CalculateHorizontalOverlap(activeWindow.Rect, window.Rect);

    //        // Prioritize windows with larger border overlap
    //        // If overlap is the same, prefer closer distance
    //        if (overlapSize > bestOverlapSize || (overlapSize == bestOverlapSize && distance < bestDistance))
    //        {
    //            bestMatch = window;
    //            bestOverlapSize = overlapSize;
    //            bestDistance = distance;
    //        }
    //    }

    //    return bestMatch;
    //}

    public static bool SwapWindows(Window window1, Window window2)
    {
        if (window1.IsFullscreen || window2.IsFullscreen)
        {
            Logger.Debug("Cannot swap maximized windows. Please restore windows to normal size first.");
            return false;
        }

        // Calculate border offsets for each window
        // These offsets account for invisible window borders/shadows
        int leftOffset1 = window1.Rect.left - window1.ExtendedRect.left;
        int topOffset1 = window1.Rect.top - window1.ExtendedRect.top;
        int widthOffset1 = (window1.ExtendedRect.right - window1.ExtendedRect.left) - (window1.Rect.right - window1.Rect.left);
        int heightOffset1 = (window1.ExtendedRect.bottom - window1.ExtendedRect.top) - (window1.Rect.bottom - window1.Rect.top);

        int leftOffset2 = window2.Rect.left - window2.ExtendedRect.left;
        int topOffset2 = window2.Rect.top - window2.ExtendedRect.top;
        int widthOffset2 = (window2.ExtendedRect.right - window2.ExtendedRect.left) - (window2.Rect.right - window2.Rect.left);
        int heightOffset2 = (window2.ExtendedRect.bottom - window2.ExtendedRect.top) - (window2.Rect.bottom - window2.Rect.top);

        Logger.Debug($"Window1 Offsets: left={leftOffset1}, top={topOffset1}, width={widthOffset1}, height={heightOffset1}. Title: {window1.Text}");
        Logger.Debug($"Window2 Offsets: left={leftOffset2}, top={topOffset2}, width={widthOffset2}, height={heightOffset2}. Title: {window2.Text}");
        
        // Position window1 where window2's visible rect was
        // Adjust for window1's own border offsets to ensure visible areas align
        int targetX1 = window2.Rect.left - leftOffset1;
        int targetY1 = window2.Rect.top - topOffset1;
        int targetWidth1 = (window2.Rect.right - window2.Rect.left) + widthOffset1;
        int targetHeight1 = (window2.Rect.bottom - window2.Rect.top) + heightOffset1;

        // Position window2 where window1's visible rect was
        // Adjust for window2's own border offsets to ensure visible areas align
        int targetX2 = window1.Rect.left - leftOffset2;
        int targetY2 = window1.Rect.top - topOffset2;
        int targetWidth2 = (window1.Rect.right - window1.Rect.left) + widthOffset2;
        int targetHeight2 = (window1.Rect.bottom - window1.Rect.top) + heightOffset2;

        // Perform the swap using SetWindowPos
        bool result1 = PInvoke.SetWindowPos(
            window1.Handle,
            HWND.Null,
            targetX1,
            targetY1,
            targetWidth1,
            targetHeight1,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
        );

        bool result2 = PInvoke.SetWindowPos(
            window2.Handle,
            HWND.Null,
            targetX2,
            targetY2,
            targetWidth2,
            targetHeight2,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
        );

        return result1 && result2;
    }

    //// Z-order filtering is implemented in GetAdjacentWindow() to prefer visible windows.
    //// Windows enumerated first (higher Z-order) are preferred unless a lower Z-order window
    //// is significantly closer (by more than Z_ORDER_BIAS pixels).
    //private static List<Window> GetWindowsOnMonitor(HMONITOR targetMonitor)
    //{
    //    var windows = new List<Window>();
    //    int zOrder = 0; // Track Z-order position (0 = topmost)

    //    PInvoke.EnumWindows((hWnd, lParam) =>
    //    {
    //        if (PInvoke.IsWindowVisible(hWnd))
    //        {
    //            // check monitor BEFORE getting full window info
    //            HMONITOR windowMonitor = PInvoke.MonitorFromWindow(hWnd, 
    //                MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

    //            if (windowMonitor == targetMonitor)
    //            {
    //                var windowInfo = Window.FromHandle(hWnd, zOrder);
    //                if (windowInfo != null && !string.IsNullOrWhiteSpace(windowInfo.Text))
    //                {
    //                    windows.Add(windowInfo);

    //                    // Only increment Z-order for non-ignored windows

    //                    // todo: verify, introduce more elaborate filtering?
    //                    if (windowInfo.ClassName != "ClunkyBordersOverlayClass")
    //                    {
    //                        zOrder++;
    //                    }
    //                }
    //            }
    //        }
    //        return true;
    //    }, 0);

    //    return windows;
    //}

    //private static int CalculateLeftProximity(Window activeWindow, Window candidateWindow)
    //{
    //    // Check if candidate window is on the left side
    //    // candidate's right edge should be near active's left edge
    //    int distance = Math.Abs(candidateWindow.Rect.right - activeWindow.Rect.left);

    //    // Also check vertical overlap
    //    if (!HasVerticalOverlap(activeWindow.Rect, candidateWindow.Rect))
    //        return -1;

    //    return distance;
    //}

    //private static int CalculateRightProximity(Window activeWindow, Window candidateWindow)
    //{
    //    // Check if candidate window is on the right side
    //    // candidate's left edge should be near active's right edge
    //    int distance = Math.Abs(candidateWindow.Rect.left - activeWindow.Rect.right);

    //    // Also check vertical overlap
    //    if (!HasVerticalOverlap(activeWindow.Rect, candidateWindow.Rect))
    //        return -1;

    //    return distance;
    //}

    //private static int CalculateUpProximity(Window activeWindow, Window candidateWindow)
    //{
    //    // Check if candidate window is above
    //    // candidate's bottom edge should be near active's top edge
    //    int distance = Math.Abs(candidateWindow.Rect.bottom - activeWindow.Rect.top);

    //    // Also check horizontal overlap
    //    if (!HasHorizontalOverlap(activeWindow.Rect, candidateWindow.Rect))
    //        return -1;

    //    return distance;
    //}

    //private static int CalculateDownProximity(Window activeWindow, Window candidateWindow)
    //{
    //    // Check if candidate window is below
    //    // candidate's top edge should be near active's bottom edge
    //    int distance = Math.Abs(candidateWindow.Rect.top - activeWindow.Rect.bottom);

    //    // Also check horizontal overlap
    //    if (!HasHorizontalOverlap(activeWindow.Rect, candidateWindow.Rect))
    //        return -1;

    //    return distance;
    //}

    //private static bool HasVerticalOverlap(RECT rect1, RECT rect2)
    //{
    //    // Check if there's any vertical overlap between two windows
    //    return rect1.top < rect2.bottom && rect1.bottom > rect2.top;
    //}

    //private static bool HasHorizontalOverlap(RECT rect1, RECT rect2)
    //{
    //    // Check if there's any horizontal overlap between two windows
    //    return rect1.left < rect2.right && rect1.right > rect2.left;
    //}

    //private static int CalculateVerticalOverlap(RECT rect1, RECT rect2)
    //{
    //    // Calculate the size of vertical overlap between two windows
    //    int overlapTop = Math.Max(rect1.top, rect2.top);
    //    int overlapBottom = Math.Min(rect1.bottom, rect2.bottom);

    //    if (overlapTop >= overlapBottom)
    //        return 0; // No overlap

    //    return overlapBottom - overlapTop;
    //}

    //private static int CalculateHorizontalOverlap(RECT rect1, RECT rect2)
    //{
    //    // Calculate the size of horizontal overlap between two windows
    //    int overlapLeft = Math.Max(rect1.left, rect2.left);
    //    int overlapRight = Math.Min(rect1.right, rect2.right);

    //    if (overlapLeft >= overlapRight)
    //        return 0; // No overlap

    //    return overlapRight - overlapLeft;
    //}

    //private static int CalculateRectOverlapArea(RECT rect1, RECT rect2)
    //{
    //    // Calculate the area of overlap between two rectangles
    //    int overlapLeft = Math.Max(rect1.left, rect2.left);
    //    int overlapRight = Math.Min(rect1.right, rect2.right);
    //    int overlapTop = Math.Max(rect1.top, rect2.top);
    //    int overlapBottom = Math.Min(rect1.bottom, rect2.bottom);

    //    if (overlapLeft >= overlapRight || overlapTop >= overlapBottom)
    //        return 0; // No overlap

    //    return (overlapRight - overlapLeft) * (overlapBottom - overlapTop);
    //}

    ///// <summary>
    ///// Check if the candidate window is significantly overlapped by any window in front of it
    ///// Shadow threshold - DPI-aware (~7px at 100% DPI, scales with DPI)
    ///// </summary>
    //private static int GetShadowThreshold(uint dpi) => (int)(7 * (dpi / 96.0));

    //private static bool IsObscuredByOtherWindows(Window candidateWindow, List<Window> allWindows)
    //{
    //    int shadowThreshold = GetShadowThreshold(candidateWindow.DPI);

    //    foreach (var window in allWindows)
    //    {
    //        // Skip the candidate itself
    //        if (window.Handle == candidateWindow.Handle)
    //            continue;

    //        // Skip ignored processes
    //        // todo: verify, introduce more elaborate filtering? - unify
    //        if (window.ClassName == "ClunkyBordersOverlayClass")
    //            continue;

    //        // Only check windows that are IN FRONT of the candidate
    //        // (lower Z-order number = more visible/on top)
    //        if (window.ZOrder >= candidateWindow.ZOrder)
    //            continue; // This window is behind the candidate, can't obscure it

    //        // Calculate overlap area
    //        int overlapArea = CalculateRectOverlapArea(candidateWindow.Rect, window.Rect);
    //        if (overlapArea == 0)
    //            continue; // No overlap

    //        // Calculate the dimensions of the overlap
    //        int overlapLeft = Math.Max(candidateWindow.Rect.left, window.Rect.left);
    //        int overlapRight = Math.Min(candidateWindow.Rect.right, window.Rect.right);
    //        int overlapTop = Math.Max(candidateWindow.Rect.top, window.Rect.top);
    //        int overlapBottom = Math.Min(candidateWindow.Rect.bottom, window.Rect.bottom);

    //        int overlapWidth = overlapRight - overlapLeft;
    //        int overlapHeight = overlapBottom - overlapTop;

    //        // If both dimensions of the overlap exceed shadow threshold, the candidate is significantly obscured
    //        if (overlapWidth > shadowThreshold && overlapHeight > shadowThreshold)
    //        {
    //            return true; // Obscured
    //        }
    //    }

    //    return false; // Not obscured
    //}

}
