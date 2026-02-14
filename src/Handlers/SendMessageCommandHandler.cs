using ShiftyGrid.Common;
using ShiftyGrid.Server;

namespace ShiftyGrid.Handlers;

internal class SendMessageCommandHandler : IRequestHandler
{
    public Response Handle(Request request)
    {
        if (request.Args.Length > 0)
        {
            var message = string.Join(" ", request.Args);
            Console.WriteLine($"[IPC Message] {message}");
            Logger.Info($"Message received: {message}");
            return Response.CreateSuccess($"Message displayed: {message}");
        }

        return Response.CreateError("No message provided");
    }
}
