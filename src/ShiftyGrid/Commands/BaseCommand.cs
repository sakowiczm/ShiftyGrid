using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Commands;

// todo: not every one server command will be exposed as cli command
// todo: command can be triggered also only by keyboard shortcut

public abstract class BaseCommand
{
    protected async Task SendRequestAsync<T>(string actionDescription, string command, T data)
    {
        var jsonData = JsonSerializer.SerializeToElement(data, GetJsonTypeInfo<T>());

        var request = new Request
        {
            Command = command,
            Data = jsonData
        };

        await SendRequestAsync(actionDescription, request);
    }

    protected async Task SendRequestAsync(string actionDescription, string command)
    {
        var request = new Request
        {
            Command = command,
            Data = null
        };

        await SendRequestAsync(actionDescription, request);
    }

    internal async Task SendRequestAsync(string actionDescription, Request request)
    {
        using var client = new IpcClient();

        if (!await IsServerRunningAsync(client))
        {
            Console.WriteLine("Error: ShiftyGrid server is not running. Please start the server first.");
            return;
        }

        Logger.Info(actionDescription);

        var response = await client.SendRequestAsync(request);

        if (response.Success)
        {
            Logger.Info($"Success: {response.Message}");
        }
        else
        {
            Logger.Info($"Error: {response.Message}");
            return;
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

    protected async Task<bool> IsServerRunningAsync(IpcClient client)
    {
        try
        {
            // tood: have dedicated health check command
            var request = new Request { Command = "status" };
            var response = await client.SendRequestAsync(request);
            
            return response.Success;
        }
        catch (Exception ex)
        {
            Logger.Error($"Server health check failed: {ex.Message}");
            return false;
        }
    }
}
