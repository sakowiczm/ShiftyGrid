using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.HiDpi;

namespace ShiftyGrid.Common;

internal static class DpiManager
{
    /// <summary>
    /// Set per-monitor DPI awareness V2 for correct coordinate handling on multi-monitor setups
    /// This must be called before any window operations
    /// </summary>
    public static void Enable()
    {
        try
        {
            var result = PInvoke.SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

            if (result == false)
                Logger.Error($"BorderRenderer. Error setting DPI awarness. Error Code: {Marshal.GetLastWin32Error()}");
            else
                Logger.Info("BorderRenderer. DPI awarness enabled");
        }
        catch (Exception ex)
        {
            Logger.Error("BorderRenderer. Error enabling DPI awarness", ex);
        }
    }
}