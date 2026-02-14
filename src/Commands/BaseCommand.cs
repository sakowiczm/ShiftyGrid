using ShiftyGrid.Common;
using ShiftyGrid.Server;

namespace ShiftyGrid.Commands;

// todo: not every one server command will be exposed as cli command
// todo: command can be triggered also only by keyboard shortcut
// todo: unify / minimize logging

/// <summary>
/// Base class for client commands that communicate with the server via IPC
/// </summary>
public abstract class BaseCommand
{
    protected void SendRequest(string actionDescription, Request message)
    {
        // todo: improve server not running detection - sync over async issue

        using var client = new IpcClient();

        if (!IsServerRunning(client))
        {
            Console.WriteLine("Error: ShiftyGrid server is not running. Please start the server first.");
            Environment.Exit(1);
        }

        Console.WriteLine(actionDescription);
        Logger.Initialize();
        
        var response = client.SendRequest(message);

        if (response.Success)
        {
            Console.WriteLine($"Success: {response.Message}");
        }
        else
        {
            Console.WriteLine($"Error: {response.Message}");
            Environment.Exit(1);
        }
    }

    protected bool IsServerRunning(IpcClient client)
    {
        try
        {
            // tood: have dedicated health check command
            var testRequest = new Request { Command = "status" };
            var response = client.SendRequest(testRequest);
            return response.Success;
        }
        catch (Exception ex)
        {
            Logger.Debug($"Server health check failed: {ex.Message}");
            return false;
        }
    }
}