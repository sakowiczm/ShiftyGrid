using ShiftyGrid.Server;

namespace ShiftyGrid.Handlers;

internal class StatusCommandHandler : IRequestHandler
{
    public Response Handle(Request request)
    {
        var uptime = Environment.TickCount64.ToString();

        return Response.CreateSuccess("Server is running", new Dictionary<string, string>
        {
            ["Status"] = "Active",
            ["Uptime"] = uptime
        });
    }
}
