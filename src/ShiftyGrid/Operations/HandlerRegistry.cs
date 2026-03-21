using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using ShiftyGrid.Operations.Commands;
//using ShiftyGrid.Infrastructure.Display;
using ShiftyGrid.Common;
using ShiftyGrid.Infrastructure.Models;

namespace ShiftyGrid.Operations.Handlers;

internal class HandlerRegistry : IRequestHandler
{
    private readonly Dictionary<string, IRequestHandler> _handlers = new();
    private readonly Func<bool> _getShouldExit;

    public HandlerRegistry(Action<bool> setShouldExit, Func<bool> getShouldExit, uint mainThreadId, ShiftyGridConfig config)
    {
        _getShouldExit = getShouldExit;

        // Create shared services
        var windowMatcher = new WindowMatcher(config);
        //var windowValidator = new WindowValidator(windowMatcher);
        //var monitorManager = new MonitorManager();
        var WindowNavigationService = new WindowNavigationService(windowMatcher, config.General.ProximityThreshold);
        var windowSelector = new WindowSelector(WindowNavigationService, windowMatcher);
        int gap = config.General.Gap;
        var windowOrganizer = new WindowOrganizer(windowMatcher, gap);

        // Register handlers
        _handlers[ExitCommand.Name] = new ExitCommandHandler(setShouldExit, getShouldExit, mainThreadId);
        _handlers[StatusCommand.Name] = new StatusCommandHandler();
        _handlers[MoveCommand.Name] = new MoveCommandHandler(gap);
        _handlers[SwapCommand.Name] = new SwapCommandHandler(WindowNavigationService);
        var grid = Grid.Parse(config.General.Grid ?? "12x12");
        _handlers[ResizeCommand.Name] = new ResizeCommandHandler(WindowNavigationService, gap, grid);
        _handlers[PromoteCommand.Name] = new PromoteCommandHandler(gap);
        _handlers[OrganizeCommand.Name] = new OrganizeCommandHandler(windowOrganizer, WindowNavigationService);
        _handlers[FocusCommand.Name] = new FocusCommandHandler(WindowNavigationService, windowMatcher);
        _handlers[ArrangeCommand.Name] = new ArrangeCommandHandler(windowSelector, gap, grid);
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
