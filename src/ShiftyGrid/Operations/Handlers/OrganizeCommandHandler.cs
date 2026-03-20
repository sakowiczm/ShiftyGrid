using ShiftyGrid.Common;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ShiftyGrid.Operations.Handlers;

public record OrganizeOptions(
    [property: JsonPropertyName("all")] bool All,
    [property: JsonPropertyName("hwnd")] string? Hwnd = null
);

internal class OrganizeCommandHandler : RequestHandler<OrganizeOptions>
{
    private readonly WindowOrganizer _organizer;
    private readonly WindowNavigationService _WindowNavigationService;

    public OrganizeCommandHandler(WindowOrganizer organizer, WindowNavigationService WindowNavigationService)
    {
        _organizer = organizer ?? throw new ArgumentNullException(nameof(organizer));
        _WindowNavigationService = WindowNavigationService ?? throw new ArgumentNullException(nameof(WindowNavigationService));
    }

    protected override Response Handle(OrganizeOptions data)
    {
        try
        {
            var result = Execute(data.All, data.Hwnd);
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

    protected override JsonTypeInfo<OrganizeOptions> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.OrganizeOptions;
    }

    private (bool Success, string Message) Execute(bool all, string? hwnd)
    {
        Logger.Info($"OrganizeCommand: Starting window organization");

        var foregroundHandle = PInvoke.GetForegroundWindow();

        // Get windows based on mode
        List<Window> windows;
        if (hwnd != null)
        {
            if (!nint.TryParse(hwnd, out var handle))
                return (false, $"Invalid window handle: {hwnd}");
            var window = Window.FromHandle(new HWND(handle), zOrder: 0);
            if (window == null)
                return (false, $"Window not found for handle: {hwnd}");
            windows = [window];
        }
        else if (all)
        {
            windows = _WindowNavigationService.GetAllWindows();
        }
        else
        {
            windows = Window.GetForeground() is Window fgWindow ? [fgWindow] : [];
        }

        Logger.Info($"OrganizeCommand: Found {windows.Count} window(s)");

        var (successCount, failedCount, matchedCount) = _organizer.OrganizeWindows(windows);

        // Restore foreground window if it changed
        if (!foregroundHandle.IsNull)
        {
            RestoreForegroundWindow(foregroundHandle);
        }

        Logger.Info($"OrganizeCommand: Completed - {successCount} success, {failedCount} failed, {matchedCount} total matched");

        if (matchedCount == 0)
            return (true, "No matching windows found to organize");

        if (failedCount > 0)
            return (true, $"Organized {successCount} window(s), {failedCount} failed");

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


}
