using ShiftyGrid.Server;

namespace ShiftyGrid.Handlers;

internal interface IRequestHandler
{
    Response Handle(Request request);
}

internal interface IRequestHandler<T> : IRequestHandler
{
    Response Handle(Request<T> request);
}