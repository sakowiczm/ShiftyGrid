using ShiftyGrid.Common;
using ShiftyGrid.Server;

namespace ShiftyGrid.Handlers;

internal class SendMessageCommandHandler : IRequestHandler<string>
{
    public Response Handle(Request<string> request)
    {
        if (!string.IsNullOrEmpty(request.Data))
        {
            Console.WriteLine($"[IPC Message] {request.Data}");
            Logger.Info($"Message received: {request.Data}");
            return Response.CreateSuccess($"Message displayed: {request.Data}");
        }

        return Response.CreateError("No message provided");
    }

    // todo: clean handlers necessity
    Response IRequestHandler.Handle(Request request)
    {
        return request is Request<string> stringRequest
            ? Handle(stringRequest)
            : Response.CreateError($"Invalid request type for {nameof(SendMessageCommandHandler)}");
    }
}
