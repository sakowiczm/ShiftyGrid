using ShiftyGrid.Commands;
using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;

namespace ShiftyGrid.Handlers;

internal class HandlerRegistry : IRequestHandler
{
    private readonly Dictionary<string, IRequestHandler> _handlers = new();
    private readonly Func<bool> _getShouldExit;

    public HandlerRegistry(Action<bool> setShouldExit, Func<bool> getShouldExit, uint mainThreadId, ShiftyGridConfig config)
    {
        _getShouldExit = getShouldExit;

        // Create shared services
        var windowMatcher = new WindowMatcher(config);
        var windowNeighborHelper = new WindowNeighborHelper(windowMatcher, config.General.ProximityThreshold);
        var windowSelector = new WindowSelector(windowNeighborHelper, windowMatcher);
        int gap = config.General.Gap;

        // Register handlers
        _handlers[ExitCommand.Name] = new ExitCommandHandler(setShouldExit, getShouldExit, mainThreadId);
        _handlers[StatusCommand.Name] = new StatusCommandHandler();
        _handlers[MoveCommand.Name] = new MoveCommandHandler(gap);
        _handlers[SwapCommand.Name] = new SwapCommandHandler(windowNeighborHelper);
        _handlers[ResizeCommand.Name] = new ResizeCommandHandler(windowNeighborHelper, gap);
        _handlers[PromoteCommand.Name] = new PromoteCommandHandler(gap);
        _handlers[OrganizeCommand.Name] = new OrganizeCommandHandler(windowMatcher, windowNeighborHelper, gap);
        _handlers[FocusCommand.Name] = new FocusCommandHandler(windowNeighborHelper, windowMatcher);
        _handlers[ArrangeCommand.Name] = new ArrangeCommandHandler(windowSelector, gap);
    }

    public Response Handle(Request request)
    {
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
