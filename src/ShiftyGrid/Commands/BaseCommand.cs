using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Commands;

// todo: not every one server command will be exposed as cli command
// todo: command can be triggered also only by keyboard shortcut
// todo: unify / minimize logging

public abstract class BaseCommand
{
    protected void SendRequest<T>(string actionDescription, string command, T data)
    {
        var jsonData = JsonSerializer.SerializeToElement(data, GetJsonTypeInfo<T>());

        var request = new Request
        {
            Command = command,
            Data = jsonData
        };

        SendRequest(actionDescription, request);
    }

    protected void SendRequest(string actionDescription, string command)
    {
        var request = new Request
        {
            Command = command,
            Data = null
        };

        SendRequest(actionDescription, request);
    }

    internal void SendRequest(string actionDescription, Request request)
    {
        // todo: improve server not running detection - sync over async issue
        //  SendRequestAsync quicker to retur error?

        using var client = new IpcClient();

        if (!IsServerRunning(client))
        {
            Console.WriteLine("Error: ShiftyGrid server is not running. Please start the server first.");
            Environment.Exit(1);
        }

        Console.WriteLine(actionDescription);
        Logger.Initialize();

        var response = client.SendRequest(request);

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

    /// <summary>
    /// Gets the JsonTypeInfo for AOT-safe serialization
    /// </summary>
    private JsonTypeInfo<T> GetJsonTypeInfo<T>()
    {
        if (typeof(T) == typeof(Position))
            return (JsonTypeInfo<T>)(object)IpcJsonContext.Default.Position;
        if (typeof(T) == typeof(string))
            return (JsonTypeInfo<T>)(object)IpcJsonContext.Default.String;

        throw new NotSupportedException($"Type {typeof(T).Name} is not registered in IpcJsonContext");
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
