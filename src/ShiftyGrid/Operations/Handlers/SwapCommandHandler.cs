using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Common;

namespace ShiftyGrid.Operations.Handlers;

internal class SwapCommandHandler : RequestHandler<Direction>
{
    private readonly WindowNavigationService _WindowNavigationService;

    public SwapCommandHandler(WindowNavigationService WindowNavigationService)
    {
        _WindowNavigationService = WindowNavigationService ?? throw new ArgumentNullException(nameof(WindowNavigationService));
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

        var adjacentWindow = _WindowNavigationService.GetAdjacentWindow(activeWindow, direction);
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
        // Calculate border offsets for both windows
        var offsets1 = WindowBorderService.CalculateOffsets(window1);
        int widthOffset1 = WindowBorderService.CalculateWidthOffset(window1);
        int heightOffset1 = WindowBorderService.CalculateHeightOffset(window1);

        var offsets2 = WindowBorderService.CalculateOffsets(window2);
        int widthOffset2 = WindowBorderService.CalculateWidthOffset(window2);
        int heightOffset2 = WindowBorderService.CalculateHeightOffset(window2);

        Logger.Debug($"Window1 Offsets: left={offsets1.Left}, top={offsets1.Top}, width={widthOffset1}, height={heightOffset1}. Title: {window1.Text}");
        Logger.Debug($"Window2 Offsets: left={offsets2.Left}, top={offsets2.Top}, width={widthOffset2}, height={heightOffset2}. Title: {window2.Text}");
        
        // Position window1 where window2's visible rect was
        var (targetX1, targetY1, targetWidth1, targetHeight1) = WindowBorderService.ApplyOffsets(
            window2.Rect.left, window2.Rect.top, window2.Rect.Width(), window2.Rect.Height(), offsets1);

        // Position window2 where window1's visible rect was
        var (targetX2, targetY2, targetWidth2, targetHeight2) = WindowBorderService.ApplyOffsets(
            window1.Rect.left, window1.Rect.top, window1.Rect.Width(), window1.Rect.Height(), offsets2);

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
