using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Common;

namespace ShiftyGrid.Windows;

public class WindowPositioner
{
    internal readonly record struct EdgeContext(
        bool LeftEdge,    // true if left edge at screen boundary
        bool RightEdge,   // true if right edge at screen boundary
        bool TopEdge,     // true if top edge at screen boundary
        bool BottomEdge   // true if bottom edge at screen boundary
    );

    /// <summary>
    /// Positions the foreground window to a specified coordinates
    /// </summary>
    public static bool ChangePosition(Coordinates coordinates, int gap)
    {
        var window = Window.GetForeground();

        return window != null && ChangePosition(window, coordinates, gap);
    }

    /// <summary>
    /// Converts a window's pixel position to grid coordinates.
    /// Rounds to nearest grid point for windows not aligned to grid.
    /// Enforces minimum size of grid unit.
    /// </summary>
    /// <param name="window">The window to convert</param>
    /// <param name="grid">The grid system to use</param>
    /// <returns>Coordinates in grid space</returns>
    internal static Coordinates ConvertWindowToGridPosition(Window window, Grid grid)
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

        return new Coordinates(grid, startX, startY, endX, endY);
    }

    private static EdgeContext DetermineEdgeContext(Window window, Coordinates coordinates)
    {
        const int EDGE_TOLERANCE = 2; // pixels

        var monitor = window.MonitorRect;
        var grid = coordinates.Grid;
        var (startX, startY, endX, endY) = coordinates;

        // Calculate target visual bounds
        var monitorWidth = monitor.Width();
        var monitorHeight = monitor.Height();
        var visualLeft = monitor.left + (monitorWidth * startX / grid.Columns);
        var visualTop = monitor.top + (monitorHeight * startY / grid.Rows);
        var visualRight = monitor.left + (monitorWidth * endX / grid.Columns);
        var visualBottom = monitor.top + (monitorHeight * endY / grid.Rows);

        return new EdgeContext(
            LeftEdge: Math.Abs(visualLeft - monitor.left) <= EDGE_TOLERANCE,
            RightEdge: Math.Abs(visualRight - monitor.right) <= EDGE_TOLERANCE,
            TopEdge: Math.Abs(visualTop - monitor.top) <= EDGE_TOLERANCE,
            BottomEdge: Math.Abs(visualBottom - monitor.bottom) <= EDGE_TOLERANCE
        );
    }

    private static (int x, int y, int width, int height) ApplyContextAwareGap(
        int visualX, int visualY, int visualWidth, int visualHeight,
        EdgeContext context, int gap)
    {
        // Calculate gap for each edge based on context
        int leftGap = context.LeftEdge ? gap : gap / 2;
        int rightGap = context.RightEdge ? gap : gap / 2;
        int topGap = context.TopEdge ? gap : gap / 2;
        int bottomGap = context.BottomEdge ? gap : gap / 2;

        return (
            x: visualX + leftGap,
            y: visualY + topGap,
            width: visualWidth - leftGap - rightGap,
            height: visualHeight - topGap - bottomGap
        );
    }

    internal static unsafe bool ChangePosition(Window window, Coordinates coordinates, int gap)
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

        var offsets = WindowBorderService.CalculateOffsets(window);
        Logger.Debug($"Invisible border offsets - Left: {offsets.Left}, Top: {offsets.Top}, Right: {offsets.Right}, Bottom: {offsets.Bottom}");

        var (startX, startY, endX, endY) = coordinates;
        Logger.Debug($"Grid position: ({startX},{startY}) to ({endX},{endY})");

        var monitorWidth = window.MonitorRect.Width();
        var monitorHeight = window.MonitorRect.Height();

        // Calculate desired visual position
        var visualX = window.MonitorRect.left + (monitorWidth * startX / coordinates.Grid.Columns);
        var visualY = window.MonitorRect.top + (monitorHeight * startY / coordinates.Grid.Rows);
        var visualWidth = monitorWidth * (endX - startX) / coordinates.Grid.Columns;
        var visualHeight = monitorHeight * (endY - startY) / coordinates.Grid.Rows;

        // Determine edge context and apply context-aware gap
        var edgeContext = DetermineEdgeContext(window, coordinates);
        var (gapX, gapY, gapWidth, gapHeight) = ApplyContextAwareGap(
            visualX, visualY, visualWidth, visualHeight, edgeContext, gap);

        // Adjust for invisible borders to achieve visual alignment
        var (x, y, width, height) = WindowBorderService.ApplyOffsets(gapX, gapY, gapWidth, gapHeight, offsets);

        Logger.Debug($"Visual position: ({visualX}, {visualY}) size: {visualWidth}x{visualHeight}");
        Logger.Debug($"Edge context: L={edgeContext.LeftEdge} R={edgeContext.RightEdge} T={edgeContext.TopEdge} B={edgeContext.BottomEdge}");
        Logger.Debug($"With context-aware gap: ({gapX}, {gapY}) size: {gapWidth}x{gapHeight}");
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

            Logger.Info($"Window positioned successfully to coordinates {coordinates}");
        }
        else
        {
            Logger.Error($"Failed to position window to coordinates {coordinates}");
        }

        return result;
    }

}
