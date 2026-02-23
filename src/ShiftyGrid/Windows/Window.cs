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

    public RECT ExtendedRect { get; init; }
      
    public RECT MonitorRect { get; init; }

    public WindowState State { get; init; }

    public bool IsParent { get; init; }

    public uint DPI { get; init; }

    // Position in Z-order stack (0 = topmost)
    // todo: make nullable? when not initialized?
    public int ZOrder { get; init; }

    public required HMONITOR MonitorHandle { get; init; }

    public bool IsFullscreen { get; init; }

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
        return State == WindowState.Normal && IsParent && !Rect.IsEmpty && !IsFullscreen;
    }

    public override string ToString()
    {
        return $$"""
                Window ({Handle}): 
                    Class Name: {(string.IsNullOrEmpty(ClassName) ? "FAIL" : ClassName)} 
                    Text: {(string.IsNullOrEmpty(Text) ? "FAIL" : Text)}
                    State: {State.ToString()}
                    IsParent: {IsParent}
                    DPI: {DPI}
                    Monitor: {MonitorHandle.Value:X}
                    IsFullscreen: {IsFullscreen}
                    ZOrder: {ZOrder}
                    Rect: {Rect.left}, {Rect.top}, {Rect.right}, {Rect.bottom}
                    ExtendedRect: {ExtendedRect.left}, {ExtendedRect.top}, {ExtendedRect.right}, {ExtendedRect.bottom}
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

    public bool IsForeground() => IsForeground(Handle);

    internal static bool IsForeground(HWND hwnd) => PInvoke.GetForegroundWindow() == hwnd;

    public bool IsValidForBorder() => IsForeground() && IsWindowReady();

    // todo: ZOrder as nullable?

    public static Window? FromHandle(HWND hwnd, int zOrder = 0)
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
            var extendedRect = GetExtendedRect(hwnd);
            var state = GetState(hwnd);
            var isParent = IsParentWindow(hwnd);
            uint dpi = PInvoke.GetDpiForWindow(hwnd);
            var hMonitor = GetWindowMonitor(hwnd);
            var monitorRect =  GetMonitor(hwnd);
           
            // todo: if state is Maximized do not calculate isFullscreen 

            var isFullscreen = IsWindowFullscreen(hwnd, hMonitor, rect);

            return new Window
            {
                Handle = hwnd,
                ClassName = className,
                Text = text,
                Rect = rect,
                ExtendedRect = extendedRect,
                MonitorRect = monitorRect,
                State = state,
                IsParent = isParent,
                DPI = dpi,
                MonitorHandle = hMonitor,
                IsFullscreen = isFullscreen,
                ZOrder = zOrder,
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"Window. Error getting window {hwnd} information.", ex);
            return null;
        }
    }

    private static HMONITOR GetWindowMonitor(HWND hwnd) => PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

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

    /// <summary>
    /// Window RECT with without shadow borders
    /// </summary>
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

            Logger.Error($"Window. Error getting window ({hwnd}) rect. Error code: {Marshal.GetLastWin32Error()}.");

            return default;
        }
        catch (Exception ex)
        {
            Logger.Error($"Window. Error getting window ({hwnd}) rect.", ex);
            return default;
        }
    }

    /// <summary>
    /// Window RECT with shadow borders
    /// </summary>
    private static RECT GetExtendedRect(HWND hwnd)
    {
        try
        {
            var result = PInvoke.GetWindowRect(hwnd, out RECT rect);

            if (result == 0)
            {
                Logger.Error($"Window. Error getting extended window ({hwnd}) rect. Error code: {Marshal.GetLastWin32Error()}.");
                return default;
            }

            return rect;
        }
        catch (Exception ex)
        {
            Logger.Error($"Window. Error getting extended window ({hwnd}) rect.", ex);
            return default;
        }
    }

    // todo: get from monitor cache

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

    private static bool IsWindowFullscreen(HWND hwnd,  HMONITOR hMonitor,  RECT windowRect)
    {
        try
        {
            // Get the monitor that contains this window
            //var hMonitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

            if (hMonitor == IntPtr.Zero)
            {
                Logger.Debug($"Window. Invalid monitor handle {hMonitor}.");
                return false;
            }

            // todo: cache monitor info at start?

            // Get monitor information
            var monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf<MONITORINFO>();

            if (!PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                Logger.Debug($"Window. Could not get monitor info. Error: {Marshal.GetLastWin32Error()}");
                return false;
            }

            // Compare window rect with monitor rect
            // A window is fullscreen if it covers the entire monitor
            var monitorRect = monitorInfo.rcMonitor;

            bool coversMonitor =
                windowRect.left <= monitorRect.left &&
                windowRect.top <= monitorRect.top &&
                windowRect.right >= monitorRect.right &&
                windowRect.bottom >= monitorRect.bottom;

            Logger.Debug($"Window. Fullscreen check for {hwnd}: Window({windowRect.left},{windowRect.top},{windowRect.right},{windowRect.bottom}) vs Monitor({monitorRect.left},{monitorRect.top},{monitorRect.right},{monitorRect.bottom}) = {coversMonitor}");

            return coversMonitor;
        }
        catch (Exception ex)
        {
            Logger.Error($"Window. Error checking fullscreen status for {hwnd}.", ex);
            return false;
        }
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