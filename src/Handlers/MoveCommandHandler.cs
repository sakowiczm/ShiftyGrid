using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;

namespace ShiftyGrid.Handlers;

// todo: this will be triggered by keyboard shortcut

public class MoveCommandHandler : IRequestHandler
{
    public Response Handle(Request request)
    {
        try
        {
            var success = WindowPositioner.ChangePosition(Position.TopRight, 2);

            return success ? Response.CreateSuccess("Window moved") : Response.CreateError("Error moving window");
        }
        catch (Exception ex)
        {
            Logger.Error("Exception moving window", ex);
            return Response.CreateError("Error moving window");
        }
    }

}