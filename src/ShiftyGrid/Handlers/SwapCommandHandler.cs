using ShiftyGrid.Common;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ShiftyGrid.Handlers;

internal class SwapCommandHandler : RequestHandler<Direction>
{
    private readonly WindowNeighborHelper _windowNeighborHelper;

    public SwapCommandHandler(WindowNeighborHelper windowNeighborHelper)
    {
        _windowNeighborHelper = windowNeighborHelper ?? throw new ArgumentNullException(nameof(windowNeighborHelper));
    }

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

    public bool Execute(Direction direction)
    {
        var activeWindow = Window.GetForeground();
        if (activeWindow == null)
            return false;

        // todo: if active window is maximized or IsFullscreen or is one of the excluded windows or is not visible etc. abandon

        var adjacentWindow = _windowNeighborHelper.GetAdjacentWindow(activeWindow, direction);
        if (adjacentWindow == null)
            return false;

        return SwapWindows(activeWindow, adjacentWindow);
    }

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

}
