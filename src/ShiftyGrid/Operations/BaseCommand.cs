using ShiftyGrid.Server;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ShiftyGrid.Infrastructure;
using ShiftyGrid.Operations.Handlers;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Common;

namespace ShiftyGrid.Operations;

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
            Console.WriteLine("ShiftyGrid server is not running. Please start the server first.");
            return;
        }

        Logger.Info(actionDescription);

        var response = await client.SendRequestAsync(request);

        if (response.Success)
        {
            Logger.Info($"Success: {response.Message}");

            if (ConsoleManager.IsAttached)
                Console.WriteLine($"Success: {response.Message}");
        }
        else
        {
            if (ConsoleManager.IsAttached)
                Logger.Info($"Error: {response.Message}");

            Logger.Info($"Error: {response.Message}");
        }
    }

    /// <summary>
    /// Gets the JsonTypeInfo for AOT-safe serialization
    /// </summary>
    private JsonTypeInfo<T> GetJsonTypeInfo<T>()
    {
        if (typeof(T) == typeof(Coordinates))
            return (JsonTypeInfo<T>)(object)IpcJsonContext.Default.Coordinates;
        if (typeof(T) == typeof(string))
            return (JsonTypeInfo<T>)(object)IpcJsonContext.Default.String;
        if (typeof(T) == typeof(ArrangeOptions))
            return (JsonTypeInfo<T>)(object)IpcJsonContext.Default.ArrangeOptions;
        if (typeof(T) == typeof(Direction))
            return (JsonTypeInfo<T>)(object)IpcJsonContext.Default.Direction;
        if (typeof(T) == typeof(Handlers.WindowResize))
            return (JsonTypeInfo<T>)(object)IpcJsonContext.Default.WindowResize;
        if (typeof(T) == typeof(Handlers.OrganizeOptions))
            return (JsonTypeInfo<T>)(object)IpcJsonContext.Default.OrganizeOptions;

        throw new NotSupportedException($"Type {typeof(T).Name} is not registered in IpcJsonContext");
    }

    protected async Task<bool> IsServerRunningAsync(IpcClient client)
    {
        try
        {
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
