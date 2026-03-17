using ShiftyGrid.Common;
using ShiftyGrid.Infrastructure;
using ShiftyGrid.Server;
using System.CommandLine;
using System.Diagnostics;

namespace ShiftyGrid.Operations.Commands;

public class ReloadCommand : BaseCommand
{
    public const string Name = "reload";

    public Command Create()
    {
        var reloadCommand = new Command(Name, "Reload configuration and restart the running instance");
        reloadCommand.SetHandler(async () => await ExecuteAsync());

        return reloadCommand;
    }

    private async Task ExecuteAsync()
    {
        using var client = new IpcClient();

        // Check if server is running
        if (!await IsServerRunningAsync(client))
        {
            Console.WriteLine("ShiftyGrid server is not running. Please start the server first.");
            return;
        }

        // Get current server parameters
        var serverInfoRequest = new Request { Command = "status" };
        var serverInfoResponse = await client.SendRequestAsync(serverInfoRequest);

        if (!serverInfoResponse.Success || serverInfoResponse.Data == null)
        {
            Console.WriteLine($"ERROR: Failed to get server information: {serverInfoResponse.Message}");
            return;
        }

        var configPath = serverInfoResponse.Data.GetValueOrDefault("ConfigPath");
        var logPath = serverInfoResponse.Data.GetValueOrDefault("LogPath");
        var logLevel = serverInfoResponse.Data.GetValueOrDefault("LogLevel", "info");
        var processIdStr = serverInfoResponse.Data.GetValueOrDefault("ProcessId");

        if (!int.TryParse(processIdStr, out int processId))
        {
            Console.WriteLine("ERROR: Invalid process ID received from server");
            return;
        }

        // Send exit command to server
        var exitRequest = new Request { Command = "exit" };
        var exitResponse = await client.SendRequestAsync(exitRequest);

        if (!exitResponse.Success)
        {
            Console.WriteLine($"ERROR: Failed to stop server: {exitResponse.Message}");
            return;
        }

        // Wait for process to exit
        if (!WaitForProcessExit(processId, timeoutSeconds: 5))
        {
            Console.WriteLine("ERROR: Server did not exit gracefully within 5 seconds.");
            Console.WriteLine("You may need to manually terminate the process and restart.");
            return;
        }

        // Start new server process
        try
        {
            var args = new List<string> { "start" };

            if (!string.IsNullOrEmpty(configPath))
            {
                args.Add("--config");
                args.Add($"\"{configPath}\"");
            }

            if (!string.IsNullOrEmpty(logPath))
            {
                args.Add("--logs");
                args.Add($"\"{logPath}\"");
            }

            args.Add("--log-level");
            args.Add(logLevel);

            ProcessSpawner.SpawnCommand(string.Join(" ", args));

            Console.WriteLine("Server restarted successfully.");
            Logger.Info("Server reload completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Failed to start new server process: {ex.Message}");
            Console.WriteLine("Please start the server manually with: ShiftyGrid.exe start");
            Logger.Error("Failed to start new server process during reload", ex);
        }
    }

    private static bool WaitForProcessExit(int processId, int timeoutSeconds = 5)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return process.WaitForExit(timeoutSeconds * 1000);
        }
        catch (ArgumentException)
        {
            // Process already exited
            return true;
        }
        catch (Exception)
        {
            // Other errors - assume process might have exited
            return true;
        }
    }

}
