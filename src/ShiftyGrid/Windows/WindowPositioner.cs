using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ShiftyGrid.Windows;

public class WindowPositioner
{
    /// <summary>
    /// Positions the foreground window to a specified position number
    /// </summary>
    public static bool ChangePosition(Position position, int gap)
    {
        var window = Window.GetForeground();

        return window != null && ChangePosition(window, position, gap);
    }

    /// <summary>
    /// Converts a window's pixel position to grid coordinates.
    /// Rounds to nearest grid point for windows not aligned to grid.
    /// Enforces minimum size of grid unit.
    /// </summary>
    /// <param name="window">The window to convert</param>
    /// <param name="grid">The grid system to use</param>
    /// <returns>Position in grid coordinates</returns>
    internal static Position ConvertWindowToGridPosition(Window window, Grid grid)
    {
        var monitor = window.MonitorRect;
        var rect = window.Rect; // Use visual rect, not ExtendedRect (shadows irrelevant to grid)

        // Calculate grid cell dimensions
        double cellWidth = monitor.Width() / (double)grid.Columns;
        double cellHeight = monitor.Height() / (double)grid.Rows;

        // Convert pixel coordinates to grid coordinates with rounding
        int startX = (int)Math.Round((rect.left - monitor.left) / cellWidth, MidpointRounding.AwayFromZero);
        int startY = (int)Math.Round((rect.top - monitor.top) / cellHeight, MidpointRounding.AwayFromZero);
        int endX = (int)Math.Round((rect.right - monitor.left) / cellWidth, MidpointRounding.AwayFromZero);
        int endY = (int)Math.Round((rect.bottom - monitor.top) / cellHeight, MidpointRounding.AwayFromZero);

        // Clamp to grid boundaries
        startX = Math.Clamp(startX, 0, grid.Columns);
        startY = Math.Clamp(startY, 0, grid.Rows);
        endX = Math.Clamp(endX, 0, grid.Columns);
        endY = Math.Clamp(endY, 0, grid.Rows);

        // Ensure minimum size (2 grid unit)
        if (endX - startX < 1) endX = startX + 2;
        if (endY - startY < 1) endY = startY + 2;

        return new Position(grid, startX, startY, endX, endY);
    }

    internal static unsafe bool ChangePosition(Window window, Position position, int gap)
    {
        Logger.Debug($"Positioning window: {window.Text} (Handle: {window.Handle})");
        Logger.Debug($"Monitor work area: ({window.MonitorRect.left}, {window.MonitorRect.top}) - ({window.MonitorRect.right}, {window.MonitorRect.bottom})");

        // Restore maximized/minimized windows to Normal state before positioning
        if (window.State != WindowState.Normal)
        {
            Logger.Debug($"Window is in {window.State} state, restoring to Normal before positioning");

            if (!window.RestoreToNormal())
            {
                Logger.Error($"Failed to restore window to Normal state, cannot position");
                return false;
            }

            // After restoring, get fresh window information since dimensions changed
            var restoredWindow = Window.FromHandle(window.Handle);
            if (restoredWindow == null)
            {
                Logger.Error("Failed to get window information after restoration");
                return false;
            }

            // Verify restoration was successful
            if (restoredWindow.State != WindowState.Normal)
            {
                Logger.Error($"Window still in {restoredWindow.State} state after restoration attempt");
                return false;
            }

            window = restoredWindow;
            Logger.Debug("Window restored successfully, proceeding with positioning");
        }

        var offsets = WindowBorderCalculator.CalculateOffsets(window);
        Logger.Debug($"Invisible border offsets - Left: {offsets.Left}, Top: {offsets.Top}, Right: {offsets.Right}, Bottom: {offsets.Bottom}");

        var (startX, startY, endX, endY) = position;
        Logger.Debug($"Grid position: ({startX},{startY}) to ({endX},{endY})");

        var monitorWidth = window.MonitorRect.Width();
        var monitorHeight = window.MonitorRect.Height();

        // Calculate desired visual position
        var visualX = window.MonitorRect.left + (monitorWidth * startX / position.Grid.Columns);
        var visualY = window.MonitorRect.top + (monitorHeight * startY / position.Grid.Rows);
        var visualWidth = monitorWidth * (endX - startX) / position.Grid.Columns;
        var visualHeight = monitorHeight * (endY - startY) / position.Grid.Rows;

        // Apply border gap
        var gapX = visualX + gap;
        var gapY = visualY + gap;
        var gapWidth = visualWidth - (gap * 2);
        var gapHeight = visualHeight - (gap * 2);

        // Adjust for invisible borders to achieve visual alignment
        var (x, y, width, height) = WindowBorderCalculator.ApplyOffsets(gapX, gapY, gapWidth, gapHeight, offsets);

        Logger.Debug($"Visual position: ({visualX}, {visualY}) size: {visualWidth}x{visualHeight}");
        Logger.Debug($"With {gap}px gap: ({gapX}, {gapY}) size: {gapWidth}x{gapHeight}");
        Logger.Debug($"Final adjusted position: ({x}, {y}) size: {width}x{height}");

        var result = PInvoke.SetWindowPos(
            window.Handle,
            HWND.Null,
            x, y, width, height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
            SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);  // Forces frame recalculation

        if (result)
        {
            // Force redraw of non-client area (title bar with close/minimize/maximize buttons)
            PInvoke.RedrawWindow(
                window.Handle,
                null,
                HRGN.Null,
                REDRAW_WINDOW_FLAGS.RDW_FRAME |
                REDRAW_WINDOW_FLAGS.RDW_INVALIDATE |
                REDRAW_WINDOW_FLAGS.RDW_UPDATENOW
            );

            Logger.Info($"Window positioned successfully to position {position}");
        }
        else
        {
            Logger.Error($"Failed to position window to position {position}");
        }

        return result;
    }

}
