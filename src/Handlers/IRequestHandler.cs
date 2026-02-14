using ShiftyGrid.Server;

namespace ShiftyGrid.Handlers;

internal interface IRequestHandler
{
    Response Handle(Request request);
}
