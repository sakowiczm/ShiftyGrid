using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace ShiftyGrid.Handlers;

internal class OrganizeCommandHandler : RequestHandler<string>
{
    private static readonly OrganizeConfig _config = OrganizeConfig.GetDefault();
    private const string IGNORED_WINDOW_CLASS = "ClunkyBordersOverlayClass";

    protected override Response Handle(string data)
    {
        try
        {
            var result = Execute();
            return result.Success
                ? Response.CreateSuccess(result.Message)
                : Response.CreateError(result.Message);
        }
        catch (Exception ex)
        {
            Logger.Error("Exception in organize command", ex);
            return Response.CreateError("Error organizing windows");
        }
    }

    protected override JsonTypeInfo<string> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.String;
    }

    private (bool Success, string Message) Execute()
    {
        Logger.Info("OrganizeCommand: Starting window organization");

        // Store foreground window to restore later
        var foregroundHandle = PInvoke.GetForegroundWindow();

        // Get current monitor (from foreground window or primary)
        HMONITOR currentMonitor;
        if (!foregroundHandle.IsNull)
        {
            currentMonitor = PInvoke.MonitorFromWindow(foregroundHandle, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        }
        else
        {
            currentMonitor = GetPrimaryMonitor();
        }

        if (currentMonitor == IntPtr.Zero)
        {
            Logger.Warning("OrganizeCommand: Could not determine current monitor");
            return (false, "Could not determine current monitor");
        }

        // Enumerate windows on current monitor
        var windows = WindowNeighborHelper.GetWindowsOnMonitor(currentMonitor);
        Logger.Info($"OrganizeCommand: Found {windows.Count} windows on monitor");

        int successCount = 0;
        int failedCount = 0;
        int matchedCount = 0;

        foreach (var window in windows)
        {
            // Skip ClunkyBorders overlay
            if (window.ClassName == IGNORED_WINDOW_CLASS)
                continue;

            // Skip minimized windows
            if (window.State == WindowState.Minimized)
                continue;

            // Check if window is ready for positioning
            if (!window.IsWindowReadyForPositioning())
            {
                Logger.Debug($"OrganizeCommand: Window '{window.Text}' not ready for positioning, skipping");
                continue;
            }

            // Skip child windows, dialogs, and popups - only organize root windows
            if (!window.IsParent)
            {
                Logger.Debug($"OrganizeCommand: Skipping child window '{window.Text}'");
                continue;
            }

            // Find matching rule
            var matcher = WindowMatcher.FindMatchingRule(window, _config);
            if (matcher == null)
            {
                Logger.Debug($"OrganizeCommand: No match for window '{window.Text}' (class: {window.ClassName})");
                continue;
            }

            matchedCount++;
            Logger.Info($"OrganizeCommand: Matched '{window.Text}' -> {matcher.Position}");

            // Apply position
            try
            {
                var positioned = WindowPositioner.ChangePosition(window, matcher.Position, Config.Gap);
                if (positioned)
                {
                    successCount++;
                }
                else
                {
                    failedCount++;
                    Logger.Warning($"OrganizeCommand: Failed to position window '{window.Text}'");
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                Logger.Error($"OrganizeCommand: Error positioning window '{window.Text}'", ex);
            }
        }

        // Restore foreground window if it changed
        if (!foregroundHandle.IsNull)
        {
            RestoreForegroundWindow(foregroundHandle);
        }

        Logger.Info($"OrganizeCommand: Completed - {successCount} success, {failedCount} failed, {matchedCount} total matched");

        if (matchedCount == 0)
        {
            return (true, "No matching windows found to organize");
        }

        if (failedCount > 0)
        {
            return (true, $"Organized {successCount} window(s), {failedCount} failed");
        }

        return (true, $"Organized {successCount} window(s)");
    }

    /// <summary>
    /// Restore foreground window using AttachThreadInput pattern
    /// </summary>
    private unsafe void RestoreForegroundWindow(HWND hwnd)
    {
        try
        {
            var currentForeground = PInvoke.GetForegroundWindow();
            if (currentForeground == hwnd)
            {
                // Already the foreground window
                return;
            }

            // Get thread IDs
            uint currentThreadId = PInvoke.GetCurrentThreadId();
            uint foregroundThreadId = PInvoke.GetWindowThreadProcessId(currentForeground, null);

            if (foregroundThreadId != currentThreadId)
            {
                // Attach to foreground thread's input queue
                PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            }

            // Set foreground window
            var success = PInvoke.SetForegroundWindow(hwnd);

            if (foregroundThreadId != currentThreadId)
            {
                // Detach from foreground thread's input queue
                PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }

            if (!success)
            {
                Logger.Debug($"OrganizeCommand: Could not restore foreground window {hwnd}");
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"OrganizeCommand: Error restoring foreground window: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the primary monitor as fallback
    /// </summary>
    private HMONITOR GetPrimaryMonitor()
    {
        try
        {
            // Use null HWND to get primary monitor
            return PInvoke.MonitorFromWindow(HWND.Null, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        }
        catch (Exception ex)
        {
            Logger.Error("OrganizeCommand: Error getting primary monitor", ex);
            return default;
        }
    }
}
