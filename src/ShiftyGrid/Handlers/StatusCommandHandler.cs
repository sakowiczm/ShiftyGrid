using ShiftyGrid.Server;

namespace ShiftyGrid.Handlers;

internal class StatusCommandHandler : IRequestHandler
{
    public Response Handle(Request request)
    {
        return Response.CreateSuccess("Server is running", new Dictionary<string, string>
        {
            ["Status"] = "Active",
            ["Uptime"] = Environment.TickCount64.ToString()
        });
    }
}
