using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Handlers;

internal class ArrangeCornersHandler : RequestHandler<string>
{
    protected override Response Handle(string data)
    {
        try
        {
            var success = Execute();
            return success
                ? Response.CreateSuccess("Windows arranged in corners")
                : Response.CreateError("Error arranging windows in corners");
        }
        catch (Exception ex)
        {
            Logger.Error("Exception arranging windows in corners", ex);
            return Response.CreateError("Error arranging windows in corners");
        }
    }

    protected override JsonTypeInfo<string> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.String;
    }

    public bool Execute()
    {
        // 1. Get active window
        var activeWindow = Window.GetForeground();
        if (activeWindow == null)
        {
            Logger.Debug("No active window found");
            return false;
        }

        if (activeWindow.IsFullscreen)
        {
            Logger.Debug("Cannot arrange maximized/fullscreen window");
            return false;
        }

        // 2. Collect windows (active + adjacent + z-order)
        var windows = CollectWindows(activeWindow, targetCount: 4);

        // 3. Apply smart positioning based on window count
        return windows.Count switch
        {
            1 => ArrangeSingleWindow(windows[0]),
            2 => ArrangeTwoWindows(windows[0], windows[1]),
            3 => ArrangeThreeWindows(windows[0], windows[1], windows[2]),
            >= 4 => ArrangeFourWindows(windows[0], windows[1], windows[2], windows[3]),
            _ => false
        };
    }

    private List<Window> CollectWindows(Window activeWindow, int targetCount)
    {
        var windows = new List<Window> { activeWindow };

        Logger.Debug($"Starting window collection for {targetCount} windows (z-order only)");
        Logger.Debug($"Active window: '{activeWindow.Text}' at ({activeWindow.Rect.left},{activeWindow.Rect.top})");

        // Simply collect top z-order windows on the same monitor
        var allWindows = WindowNeighborHelper.GetWindowsOnMonitor(activeWindow.MonitorHandle);
        Logger.Debug($"Monitor has {allWindows.Count} total windows");

        foreach (var window in allWindows)
        {
            if (windows.Count >= targetCount) break;

            // Skip if already in list (active window)
            if (windows.Any(w => w.Handle == window.Handle))
                continue;

            // Skip ClunkyBordersOverlayWindow (ShiftyGrid's own overlay)
            if (window.ClassName == "ClunkyBordersOverlayClass")
                continue;

            // Skip only minimized windows and windows without titles
            if (window.State == WindowState.Minimized || string.IsNullOrWhiteSpace(window.Text))
                continue;

            windows.Add(window);
            Logger.Debug($"Collected window '{window.Text}' (z-order: {window.ZOrder})");
        }

        Logger.Debug($"Final collection: {windows.Count} windows for corner arrangement");
        return windows;
    }

    private bool ArrangeSingleWindow(Window window)
    {
        // Position to quadrant based on center X and Y
        var monitorRect = window.MonitorRect;
        var screenCenterX = monitorRect.left + (monitorRect.right - monitorRect.left) / 2;
        var screenCenterY = monitorRect.top + (monitorRect.bottom - monitorRect.top) / 2;
        var windowCenterX = window.Rect.left + (window.Rect.right - window.Rect.left) / 2;
        var windowCenterY = window.Rect.top + (window.Rect.bottom - window.Rect.top) / 2;

        Position position;
        string quadrantName;

        if (windowCenterX < screenCenterX && windowCenterY < screenCenterY)
        {
            position = Position.LeftTop;
            quadrantName = "LeftTop";
        }
        else if (windowCenterX >= screenCenterX && windowCenterY < screenCenterY)
        {
            position = Position.RightTop;
            quadrantName = "RightTop";
        }
        else if (windowCenterX < screenCenterX && windowCenterY >= screenCenterY)
        {
            position = Position.LeftBottom;
            quadrantName = "LeftBottom";
        }
        else
        {
            position = Position.RightBottom;
            quadrantName = "RightBottom";
        }

        Logger.Debug($"Arranging single window '{window.Text}' to {quadrantName}");

        return WindowPositioner.ChangePosition(window, position, gap: 2);
    }

    private bool ArrangeTwoWindows(Window window1, Window window2)
    {
        // Use LeftHalf/RightHalf based on window center X positions
        var window1CenterX = window1.Rect.left + (window1.Rect.right - window1.Rect.left) / 2;
        var window2CenterX = window2.Rect.left + (window2.Rect.right - window2.Rect.left) / 2;

        Window leftWindow = window1CenterX < window2CenterX ? window1 : window2;
        Window rightWindow = window1CenterX < window2CenterX ? window2 : window1;

        Logger.Debug($"Arranging two windows: '{leftWindow.Text}' (left) and '{rightWindow.Text}' (right)");

        var leftResult = WindowPositioner.ChangePosition(leftWindow, Position.LeftHalf, gap: 2);
        var rightResult = WindowPositioner.ChangePosition(rightWindow, Position.RightHalf, gap: 2);
        return leftResult && rightResult;
    }

    private bool ArrangeThreeWindows(Window window1, Window window2, Window window3)
    {
        // Prioritize top row, then bottom-left
        // Sort windows by Y position (top to bottom), then by X position (left to right)
        var windows = new[] { window1, window2, window3 }
            .OrderBy(w => w.Rect.top + (w.Rect.bottom - w.Rect.top) / 2)  // Sort by center Y
            .ThenBy(w => w.Rect.left + (w.Rect.right - w.Rect.left) / 2)  // Then by center X
            .ToArray();

        // Assign positions: LeftTop, RightTop, LeftBottom
        var assignments = new List<(Window window, Position position, string name)>
        {
            (windows[0], Position.LeftTop, "LeftTop"),
            (windows[1], Position.RightTop, "RightTop"),
            (windows[2], Position.LeftBottom, "LeftBottom")
        };

        Logger.Debug($"Arranging three windows:");
        foreach (var (window, _, name) in assignments)
        {
            Logger.Debug($"  '{window.Text}' -> {name}");
        }

        // Position all windows
        bool success = true;
        foreach (var (window, position, _) in assignments)
        {
            success &= WindowPositioner.ChangePosition(window, position, gap: 2);
        }
        return success;
    }

    private bool ArrangeFourWindows(Window w1, Window w2, Window w3, Window w4)
    {
        // Sort windows by Y position (top to bottom), then X position (left to right)
        var sortedWindows = new[] { w1, w2, w3, w4 }
            .OrderBy(w => w.Rect.top + (w.Rect.bottom - w.Rect.top) / 2)  // Y first
            .ThenBy(w => w.Rect.left + (w.Rect.right - w.Rect.left) / 2)  // Then X
            .ToArray();

        // Assign to fill top row first, then bottom row
        var assignments = new[]
        {
            (sortedWindows[0], Position.LeftTop, "LeftTop"),
            (sortedWindows[1], Position.RightTop, "RightTop"),
            (sortedWindows[2], Position.LeftBottom, "LeftBottom"),
            (sortedWindows[3], Position.RightBottom, "RightBottom")
        };

        Logger.Debug($"Arranging four windows (sorted top-to-bottom, left-to-right):");
        foreach (var (window, _, name) in assignments)
        {
            Logger.Debug($"  '{window.Text}' -> {name}");
        }

        // Position all windows
        bool success = true;
        foreach (var (window, position, _) in assignments)
        {
            success &= WindowPositioner.ChangePosition(window, position, gap: 2);
        }
        return success;
    }
}
