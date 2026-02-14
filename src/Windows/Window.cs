using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using ShiftyGrid.Common;

namespace ShiftyGrid.Windows;

internal record Window
{
    public required HWND Handle { get; init; }

    public required string ClassName { get; init; }

    public required string Text { get; init; } // todo: Title?

    public RECT Rect { get; init; }
    
    public RECT Offset { get; init; } // diff DWN Rect - Rect // todo: optimize calls
    
    public RECT MonitorRect { get; init; }

    public WindowState State { get; init; }

    public bool IsParent { get; init; }

    public uint DPI { get; init; }

    public RECT GetOverlayRect(int size = 0)
    {
        if (size == 0)
            return Rect;

        return RECT.FromXYWH(
                Rect.X - size,
                Rect.Y - size,
                Rect.Width + 2 * size,
                Rect.Height + 2 * size);
    }

    public bool CanHaveBorder()
    {
        return State == WindowState.Normal && IsParent && !Rect.IsEmpty;
    }

    public override string ToString()
    {
        return $"""
                Window ({Handle}): 
                    Class Name: {(string.IsNullOrEmpty(ClassName) ? "FAIL" : ClassName)} 
                    Text: {(string.IsNullOrEmpty(Text) ? "FAIL" : Text)}
                    State: {State.ToString()}
                    IsParent: {IsParent}
                    DPI: {DPI}
                    Rect: {Rect.left}, {Rect.top}, {Rect.right}, {Rect.bottom}
                """;
    }

    public unsafe bool IsWindowReady()
    {
        // Check if window is visible
        if (!PInvoke.IsWindowVisible(Handle))
        {
            return false;
        }

        // Check if window is cloaked (hidden during animations/transitions)
        int cloaked = 0;
        var result = PInvoke.DwmGetWindowAttribute(
            Handle,
            DWMWINDOWATTRIBUTE.DWMWA_CLOAKED,
            &cloaked,
            sizeof(int)
        );

        if (result.Failed)
        {
            Logger.Warning($"WindowReadinessChecker. Failed to get cloaked attribute. Error code: {Marshal.GetLastWin32Error()}");
            // Assume ready if we can't check
            return true;
        }

        // If cloaked (non-zero), window is not ready
        return cloaked == 0;
    }

    public unsafe bool IsForeground() => IsForeground(Handle);

    internal static bool IsForeground(HWND hwnd) => PInvoke.GetForegroundWindow() == hwnd;

    public bool IsValidForBorder() => IsForeground() && IsWindowReady();

    public static Window? FromHandle(HWND hwnd)
    {
        try
        {
            if (!PInvoke.IsWindow(hwnd))
            {
                Logger.Debug($"Window. Window handle {hwnd} is no longer valid");
                return null;
            }

            var className = GetClassName(hwnd);
            var text = GetText(hwnd);
            var rect = GetRect(hwnd);
            var offset = GetOffset(hwnd);
            var state = GetState(hwnd);
            var isParent = IsParentWindow(hwnd);
            uint dpi = PInvoke.GetDpiForWindow(hwnd);
            var monitorRect =  GetMonitor(hwnd);

            return new Window
            {
                Handle = hwnd,
                ClassName = className,
                Text = text,
                Rect = rect,
                Offset = offset,
                MonitorRect = monitorRect,
                State = state,
                IsParent = isParent,
                DPI = dpi
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Window. Error getting window {hwnd} information.", ex);
            return null;
        }
    }

    public static Window? GetForeground()
    {
        try
        {
            var hwnd = PInvoke.GetForegroundWindow();

            if (hwnd.IsNull)
            {
                Logger.Error("Window. No active window.");
                return null;
            }

            return FromHandle(hwnd);
        }
        catch (Exception ex)
        {
            Logger.Error("Window. Error getting current active window.", ex);
            return null;
        }
    }

    private static unsafe RECT GetRect(HWND hwnd)
    {
        try
        {
            RECT rect;

            var hResult = PInvoke.DwmGetWindowAttribute(
                hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
                &rect,
                (uint)sizeof(RECT)
            );

            if (hResult.Succeeded)
                return rect;

            Logger.Error($"Window. Error getting extended window ({hwnd}) rect. Error code: {Marshal.GetLastWin32Error()}.");

            var result = PInvoke.GetWindowRect(hwnd, out rect);

            if (result == 0)
            {
                Logger.Error($"Window. Error getting window ({hwnd}) rect. Error code: {Marshal.GetLastWin32Error()}.");
                return default;
            }

            return rect;
        }
        catch (Exception ex)
        {
            Logger.Error($"Window. Error getting window ({hwnd}) rect.", ex);
            return default;
        }
    }
    
    private static unsafe RECT GetOffset(HWND hwnd)
    {
        // Get the actual window rectangle
        if (!PInvoke.GetWindowRect(hwnd, out var windowRect))
        {
            Logger.Error("Failed to get window rect, assuming no invisible borders");
            return default;
        }

        // Get the extended frame bounds (visible area)
        RECT extendedFrame = default;
        var result = PInvoke.DwmGetWindowAttribute(
            hwnd,
            DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS,
            &extendedFrame,
            (uint)Marshal.SizeOf<RECT>());

        if (result != 0)
        {
            Logger.Error("Failed to get extended frame bounds, assuming no invisible borders");
            return default;
        }

        // Calculate the invisible border offsets
        var offsets = new RECT
        {
            left = extendedFrame.left - windowRect.left,
            top = extendedFrame.top - windowRect.top,
            right = windowRect.right - extendedFrame.right,
            bottom = windowRect.bottom - extendedFrame.bottom
        };

        return offsets;
    }    

    private static RECT GetMonitor(HWND hwnd)
    {
        try
        {
            var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            if (monitor == default)
            {
                Logger.Error($"Window. Error getting window ({hwnd}) monitor rect. Error code: {Marshal.GetLastWin32Error()}.");
                return default;
            }

            var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
            if (PInvoke.GetMonitorInfo(monitor, ref monitorInfo)) return 
                monitorInfo.rcWork;
            
            Logger.Error($"Window. Error getting monitor info. Error code: {Marshal.GetLastWin32Error()}.");
            return default;
        }
        catch (Exception ex)
        {
            Logger.Error($"Window. Error getting window ({hwnd}) monitor rect.", ex);
            return default;
        }
    }

    private static bool IsParentWindow(HWND hwnd)
    {
        try
        {
            var rootWindow = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);
            if (rootWindow.IsNull)
                return false;

            if (rootWindow != hwnd)
                return false;

            var style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
            if ((style & (uint)WINDOW_STYLE.WS_POPUP) != 0 && (style & (uint)WINDOW_STYLE.WS_CAPTION) == 0)
                return false;

            var exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            if ((exStyle & (uint)WINDOW_EX_STYLE.WS_EX_DLGMODALFRAME) != 0)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"Window. Error checking window parent.", ex);
            return false;
        }
    }
    
    private static WindowState GetState(HWND hwnd)
    {
        var placement = new WINDOWPLACEMENT();
        placement.length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>();

        var result = PInvoke.GetWindowPlacement(hwnd, ref placement);

        if (result == 0)
        {
            Logger.Error($"Window. Error getting window state.");
            return WindowState.Unknown;
        }

        return (uint)placement.showCmd switch
        {
            0 => WindowState.Hiden,
            1 => WindowState.Normal,
            2 => WindowState.Minimized,
            3 => WindowState.Maximized,
            _ => WindowState.Unknown,
        };
    }

    private static unsafe string GetClassName(HWND hwnd)
    {
        const int maxLength = 256;
        var buffer = new char[maxLength];

        fixed (char* pBuffer = buffer)
        {
            var length = PInvoke.GetClassName(hwnd, pBuffer, maxLength);
            return length == 0 ? string.Empty : new string(pBuffer, 0, length);
        }
    }

    private static unsafe string GetText(HWND hwnd)
    {
        const int maxLength = 256;
        var buffer = new char[maxLength];

        fixed (char* pBuffer = buffer)
        {
            var length = PInvoke.GetWindowText(hwnd, pBuffer, maxLength);
            return length == 0 ? string.Empty : new string(pBuffer, 0, length);
        }
    }

}

public enum WindowState
{
    Hiden = 0,
    Normal = 1,
    Minimized = 2,
    Maximized = 3,
    Unknown = 4
};