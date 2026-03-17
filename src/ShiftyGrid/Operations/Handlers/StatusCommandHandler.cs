using ShiftyGrid.Operations.Commands;
using ShiftyGrid.Server;

namespace ShiftyGrid.Operations.Handlers;

internal class StatusCommandHandler : IRequestHandler
{
    public Response Handle(Request request)
    {
        var uptime = Environment.TickCount64.ToString();

        // todo: output to console

        var data = new Dictionary<string, string>
        {
            ["Status"] = "Active",
            ["Uptime"] = uptime,
            ["ConfigPath"] = StartCommand.ConfigPath ?? "",
            ["LogPath"] = StartCommand.LogPath ?? "",
            ["LogLevel"] = StartCommand.LogLevel,
            ["ProcessId"] = Environment.ProcessId.ToString()
        };

        return Response.CreateSuccess("Server is running", data);
    }
}
