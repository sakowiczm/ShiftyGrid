using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using ShiftyGrid.Common;
using ShiftyGrid.Windows;

namespace ShiftyGrid.Common;

/// <summary>
/// Manages monitor information with caching to improve performance.
/// Provides centralized access to monitor data across the application.
/// </summary>
internal class MonitorManager
{
    private readonly ConcurrentDictionary<HMONITOR, MonitorInfo> _cache = new();
    private static readonly object _lock = new();
    private HMONITOR? _cachedPrimaryMonitor;

    /// <summary>
    /// Information about a monitor.
    /// </summary>
    public record MonitorInfo(
        HMONITOR Handle,
        RECT WorkArea,
        RECT Bounds,
        bool IsPrimary
    );

    /// <summary>
    /// Gets monitor information for the specified monitor handle.
    /// Results are cached for performance.
    /// </summary>
    public MonitorInfo? GetMonitorInfo(HMONITOR monitor)
    {
        if (monitor == IntPtr.Zero)
        {
            Logger.Debug("MonitorManager: Invalid monitor handle");
            return null;
        }

        return _cache.GetOrAdd(monitor, m => CreateMonitorInfo(m) ?? throw new InvalidOperationException($"Failed to create monitor info for {m}"));
    }

    /// <summary>
    /// Gets the monitor that contains the specified window.
    /// </summary>
    public MonitorInfo? GetMonitorForWindow(HWND hwnd)
    {
        try
        {
            var monitor = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            return GetMonitorInfo(monitor);
        }
        catch (Exception ex)
        {
            Logger.Error($"MonitorManager: Error getting monitor for window {hwnd}", ex);
            return null;
        }
    }

    /// <summary>
    /// Gets the monitor that contains the specified window.
    /// </summary>
    public MonitorInfo? GetMonitorForWindow(Window window)
    {
        return GetMonitorInfo(window.MonitorHandle);
    }

    /// <summary>
    /// Gets the primary monitor.
    /// </summary>
    public MonitorInfo? GetPrimaryMonitor()
    {
        lock (_lock)
        {
            // Use cached primary monitor if available
            if (_cachedPrimaryMonitor.HasValue)
            {
                var cached = GetMonitorInfo(_cachedPrimaryMonitor.Value);
                if (cached != null)
                    return cached;
            }

            // Find primary monitor by enumerating all monitors in cache
            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsPrimary)
                {
                    _cachedPrimaryMonitor = kvp.Key;
                    return kvp.Value;
                }
            }

            // Primary monitor not found in cache - caller should use GetMonitorForWindow instead
            Logger.Debug("MonitorManager: Primary monitor not in cache");
            return null;
        }
    }

    /// <summary>
    /// Gets the work area (desktop area excluding taskbar) for a monitor.
    /// </summary>
    public RECT? GetWorkArea(HMONITOR monitor)
    {
        return GetMonitorInfo(monitor)?.WorkArea;
    }

    /// <summary>
    /// Invalidates the monitor cache. Call this when display settings change.
    /// </summary>
    public void InvalidateCache()
    {
        Logger.Info("MonitorManager: Invalidating monitor cache");
        _cache.Clear();
        lock (_lock)
        {
            _cachedPrimaryMonitor = null;
        }
    }

    /// <summary>
    /// Creates monitor info from a monitor handle.
    /// </summary>
    private MonitorInfo? CreateMonitorInfo(HMONITOR monitor)
    {
        try
        {
            var monitorInfo = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };

            if (!PInvoke.GetMonitorInfo(monitor, ref monitorInfo))
            {
                Logger.Error($"MonitorManager: Failed to get monitor info. Error: {Marshal.GetLastWin32Error()}");
                return null;
            }

            bool isPrimary = (monitorInfo.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY = 1

            var info = new MonitorInfo(
                Handle: monitor,
                WorkArea: monitorInfo.rcWork,
                Bounds: monitorInfo.rcMonitor,
                IsPrimary: isPrimary
            );

            Logger.Debug($"MonitorManager: Cached monitor - Primary: {isPrimary}");

            return info;
        }
        catch (Exception ex)
        {
            Logger.Error($"MonitorManager: Error creating monitor info", ex);
            return null;
        }
    }

    /// <summary>
    /// Gets the number of monitors currently cached.
    /// </summary>
    public int CachedMonitorCount => _cache.Count;
}
