using ShiftyGrid.Common;
using ShiftyGrid.Server;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Handlers;

internal class SendMessageCommandHandler : RequestHandler<string>
{
    protected override Response Handle(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return Response.CreateError("No message provided");
        }

        Console.WriteLine($"[IPC Message] {data}");
        Logger.Info($"Message received: {data}");
        return Response.CreateSuccess($"Message displayed: {data}");
    }

    protected override JsonTypeInfo<string> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.String;
    }
}
