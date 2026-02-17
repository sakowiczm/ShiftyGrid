using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ShiftyGrid.Common;
using ShiftyGrid.Configuration;

namespace ShiftyGrid.Windows;

// todo: we don't need whole configuration manager
//  we reposition based on single position
//  do we need some global Grid object or each grid position will be calculated on the fly? e.g good when switching on different monitors
        
        
// todo: something else can also supply what is the applicable foreground window

// todo: position 4 has some sizing issues


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
    
    // todo: have generic method to change window position - position calculation do elsewhere?

    internal static bool ChangePosition(Window window, Position position, int gap)
    {
        Logger.Debug($"Positioning window: {window.Text} (Handle: {window.Handle})");
        Logger.Debug($"Monitor work area: ({window.MonitorRect.left}, {window.MonitorRect.top}) - ({window.MonitorRect.right}, {window.MonitorRect.bottom})");

        // todo: offset may not need to be calculated for every window
        Logger.Debug($"Invisible border offsets - Left: {window.Offset.left}, Top: {window.Offset.top}, Right: {window.Offset.right}, Bottom: {window.Offset.bottom}");

        var (startX, startY, endX, endY) = position;
        Logger.Debug($"Grid position: ({startX},{startY}) to ({endX},{endY})");
        
        var monitorWidth = window.MonitorRect.right - window.MonitorRect.left;
        var monitorHeight = window.MonitorRect.bottom - window.MonitorRect.top;

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
        var x = gapX - window.Offset.left;
        var y = gapY - window.Offset.top;
        var width = gapWidth + window.Offset.left + window.Offset.right;
        var height = gapHeight + window.Offset.top + window.Offset.bottom;

        Logger.Debug($"Visual position: ({visualX}, {visualY}) size: {visualWidth}x{visualHeight}");
        Logger.Debug($"With {gap}px gap: ({gapX}, {gapY}) size: {gapWidth}x{gapHeight}");
        Logger.Debug($"Final adjusted position: ({x}, {y}) size: {width}x{height}");

        var result = PInvoke.SetWindowPos(
            window.Handle,
            HWND.Null,
            x, y, width, height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        if (result)
        {
            Logger.Info($"Window positioned successfully to position {position}");
        }
        else
        {
            Logger.Error($"Failed to position window to position {position}");
        }
        
        return result;
    }

}
