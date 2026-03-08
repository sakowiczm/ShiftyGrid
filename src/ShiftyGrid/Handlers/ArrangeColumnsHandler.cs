using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Handlers;


// todo: idea - arrange --cols 2 --rows 2
//  this could allow to unify 3 command - arrange, arrange columns and arrange corners
//  windows order: focused window -> adjectent windows -> any non adjecent but fully visible to the user (not obscured)
//    -> windows closest on z-order -> anything what is left

internal class ArrangeColumnsHandler : RequestHandler<string>
{
    protected override Response Handle(string data)
    {
        try
        {
            var success = Execute();
            return success
                ? Response.CreateSuccess("Windows arranged in columns")
                : Response.CreateError("Error arranging windows in columns");
        }
        catch (Exception ex)
        {
            Logger.Error("Exception arranging windows in columns", ex);
            return Response.CreateError("Error arranging windows in columns");
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
        var windows = CollectWindows(activeWindow, targetCount: 3);

        // 3. Apply smart positioning based on window count
        return windows.Count switch
        {
            1 => ArrangeSingleWindow(windows[0]),
            2 => ArrangeTwoWindows(windows[0], windows[1]),
            >= 3 => ArrangeThreeWindows(windows[0], windows[1], windows[2]),
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

        Logger.Debug($"Final collection: {windows.Count} windows for column arrangement");
        return windows;
    }

    private bool ArrangeSingleWindow(Window window)
    {
        // Position to left or right half based on center X
        var monitorRect = window.MonitorRect;
        var screenCenterX = monitorRect.left + (monitorRect.right - monitorRect.left) / 2;
        var windowCenterX = window.Rect.left + (window.Rect.right - window.Rect.left) / 2;

        var position = windowCenterX < screenCenterX ? Position.LeftHalf : Position.RightHalf;

        Logger.Debug($"Arranging single window '{window.Text}' to {(windowCenterX < screenCenterX ? "left" : "right")} half");

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
        // Sort windows by center X position (left to right)
        var sortedWindows = new[] { window1, window2, window3 }
            .OrderBy(w => w.Rect.left + (w.Rect.right - w.Rect.left) / 2)
            .ToArray();

        // Assign in order: leftmost → Col1, middle → Col2, rightmost → Col3
        var assignments = new[]
        {
            (sortedWindows[0], Position.ThreeColumnsCol1, 1),
            (sortedWindows[1], Position.ThreeColumnsCol2, 2),
            (sortedWindows[2], Position.ThreeColumnsCol3, 3)
        };

        Logger.Debug($"Arranging three windows (sorted left to right):");
        foreach (var (window, _, column) in assignments)
        {
            Logger.Debug($"  '{window.Text}' -> column {column}");
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
