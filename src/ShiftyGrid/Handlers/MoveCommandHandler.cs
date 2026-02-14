using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;

namespace ShiftyGrid.Handlers;

public class MoveCommandHandler : IRequestHandler<Position>
{
    public Response Handle(Request<Position> request)
    {
        try
        {
            // todo: Data validation?

            var success = WindowPositioner.ChangePosition(request.Data, 2);

            return success ? Response.CreateSuccess("Window moved") : Response.CreateError("Error moving window");
        }
        catch (Exception ex)
        {
            Logger.Error("Exception moving window", ex);
            return Response.CreateError("Error moving window");
        }
    }

    Response IRequestHandler.Handle(Request request)
    {
        if(request is Request<Position>)
        {
            var abc = (Request<Position>) request;
            Console.WriteLine(abc.ToString());
        }


        return request is Request<Position> positionRequest
            ? Handle(positionRequest)
            : Response.CreateError($"Invalid request type for {nameof(MoveCommandHandler)}");
    }
}