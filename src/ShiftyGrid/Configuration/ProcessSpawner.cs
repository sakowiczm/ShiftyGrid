using ShiftyGrid.Common;
using System.Diagnostics;

namespace ShiftyGrid.Configuration;

/// <summary>
/// Utility for spawning ShiftyGrid CLI commands as separate processes
/// </summary>
internal static class ProcessSpawner
{
    /// <summary>
    /// Spawns ShiftyGrid.exe with the specified command string (fire-and-forget)
    /// </summary>
    /// <param name="commandString">Full command string (e.g., "move --position 0,0,6,12")</param>
    public static void SpawnCommand(string commandString)
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine executable path");

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = commandString,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardError = true
        };

        try
        {
            using var process = Process.Start(startInfo);

            if (process == null)
            {
                Logger.Error($"Failed to spawn process for command: {commandString}");
                return;
            }

            // Fire-and-forget: don't wait for exit
            // Log errors asynchronously if process fails quickly
            _ = Task.Run(async () =>
            {
                try
                {
                    // Wait briefly to catch immediate errors
                    if (await Task.Run(() => process.WaitForExit(100)))
                    {
                        if (process.ExitCode != 0)
                        {
                            var stderr = await process.StandardError.ReadToEndAsync();
                            if (!string.IsNullOrEmpty(stderr))
                            {
                                Logger.Warning($"Command '{commandString}' failed: {stderr}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug($"Error checking command status: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to spawn command '{commandString}': {ex.Message}");
        }
    }
}
