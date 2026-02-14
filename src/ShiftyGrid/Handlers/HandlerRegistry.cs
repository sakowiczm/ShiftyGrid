using ShiftyGrid.Commands;
using ShiftyGrid.Common;
using ShiftyGrid.Server;

namespace ShiftyGrid.Handlers;

internal class HandlerRegistry : IRequestHandler
{
    private readonly Dictionary<string, IRequestHandler> _handlers = new();
    private readonly Func<bool> _getShouldExit;

    public HandlerRegistry(Action<bool> setShouldExit, Func<bool> getShouldExit, uint mainThreadId)
    {
        _getShouldExit = getShouldExit;

        // Register handlers
        _handlers[ExitCommand.Name] =  new ExitCommandHandler(setShouldExit, getShouldExit, mainThreadId);
        _handlers[SendMessageCommand.Name] = new SendMessageCommandHandler();
        _handlers[StatusCommand.Name] = new StatusCommandHandler();
        _handlers[MoveCommand.Name] = new MoveCommandHandler();
    }

    public Response Handle(Request request)
    {
        Logger.Info($"Received request: {request.Command}");

        // Don't process requests if we're already shutting down (except exit)
        var command = request.Command.ToLowerInvariant();
        if (_getShouldExit() && command != ExitCommand.Name)
        {
            return Response.CreateError("Server is shutting down");
        }

        if (_handlers.TryGetValue(command, out var handler))
        {
            return handler.Handle(request);
        }

        Logger.Warning($"Unknown command: {request.Command}");
        return Response.CreateError($"Unknown command: {request.Command}");
    }
}
