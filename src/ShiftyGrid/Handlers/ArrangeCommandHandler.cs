using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Handlers;

internal class ArrangeCommandHandler : RequestHandler<string>
{
    protected override Response Handle(string data)
    {
        try
        {
            var success = Execute();
            return success
                ? Response.CreateSuccess("Windows arranged")
                : Response.CreateError("Error arranging windows");
        }
        catch (Exception ex)
        {
            Logger.Error("Exception arranging windows", ex);
            return Response.CreateError("Error arranging windows");
        }
    }

    protected override JsonTypeInfo<string> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.String;
    }

    public bool Execute()
    {
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

        // Try to find a horizontal neighbor (left or right)
        var leftNeighbor = WindowNeighborHelper.GetAdjacentWindow(activeWindow, Direction.Left);
        var rightNeighbor = leftNeighbor == null
            ? WindowNeighborHelper.GetAdjacentWindow(activeWindow, Direction.Right)
            : null;

        var neighbor = leftNeighbor ?? rightNeighbor;

        if (neighbor != null)
        {
            // Two-window scenario: arrange both windows side by side
            return ArrangeTwoWindows(activeWindow, neighbor);
        }
        else
        {
            // Single-window scenario: position active window to left or right half
            return ArrangeSingleWindow(activeWindow);
        }
    }

    private bool ArrangeTwoWindows(Window window1, Window window2)
    {
        if (window2.IsFullscreen)
        {
            Logger.Debug("Cannot arrange with maximized/fullscreen neighbor window");
            return false;
        }

        // Calculate center X for both windows
        var monitorRect = window1.MonitorRect;
        var screenCenterX = monitorRect.left + (monitorRect.right - monitorRect.left) / 2;

        var window1CenterX = window1.Rect.left + (window1.Rect.right - window1.Rect.left) / 2;
        var window2CenterX = window2.Rect.left + (window2.Rect.right - window2.Rect.left) / 2;

        // Determine which window is on the left side of the screen
        Window leftWindow, rightWindow;
        if (window1CenterX < window2CenterX)
        {
            leftWindow = window1;
            rightWindow = window2;
        }
        else
        {
            leftWindow = window2;
            rightWindow = window1;
        }

        Logger.Debug($"Arranging two windows: '{leftWindow.Text}' (left) and '{rightWindow.Text}' (right)");

        // Position windows with 2px gap
        var leftResult = WindowPositioner.ChangePosition(leftWindow, Position.LeftHalf, gap: 2);
        var rightResult = WindowPositioner.ChangePosition(rightWindow, Position.RightHalf, gap: 2);

        return leftResult && rightResult;
    }

    private bool ArrangeSingleWindow(Window window)
    {
        // Calculate center X of the window
        var monitorRect = window.MonitorRect;
        var screenCenterX = monitorRect.left + (monitorRect.right - monitorRect.left) / 2;
        var windowCenterX = window.Rect.left + (window.Rect.right - window.Rect.left) / 2;

        // Position to left or right half based on which side of screen center the window is on
        var position = windowCenterX < screenCenterX ? Position.LeftHalf : Position.RightHalf;

        Logger.Debug($"Arranging single window '{window.Text}' to {(windowCenterX < screenCenterX ? "left" : "right")} half");

        return WindowPositioner.ChangePosition(window, position, gap: 2);
    }
}
