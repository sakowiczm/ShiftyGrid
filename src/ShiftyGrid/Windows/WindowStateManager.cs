using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace ShiftyGrid.Windows;

internal class PromotedWindowState
{
    public required HWND WindowHandle { get; init; }
    public required RECT OriginalRect { get; init; }
    public required HMONITOR MonitorHandle { get; init; }
    public required DateTime PromotedAt { get; init; }
}

internal class WindowStateManager
{
    private static WindowStateManager? _instance;
    private static readonly object _instanceLock = new();

    private readonly Dictionary<HMONITOR, PromotedWindowState> _promotedWindows = new();
    private readonly object _lock = new();

    private WindowStateManager()
    {
    }

    public static WindowStateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = new WindowStateManager();
                    }
                }
            }
            return _instance;
        }
    }

    public PromotedWindowState? GetPromotedWindow(HMONITOR monitor)
    {
        lock (_lock)
        {
            return _promotedWindows.TryGetValue(monitor, out var state) ? state : null;
        }
    }

    public void SetPromotedWindow(HMONITOR monitor, PromotedWindowState state)
    {
        lock (_lock)
        {
            _promotedWindows[monitor] = state;
        }
    }

    public void ClearPromotedWindow(HMONITOR monitor)
    {
        lock (_lock)
        {
            _promotedWindows.Remove(monitor);
        }
    }
}
