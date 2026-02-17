using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Handlers;

internal class MoveCommandHandler : RequestHandler<Position>
{
    protected override Response Handle(Position data)
    {
        try
        {
            // todo: Data validation?

            var success = WindowPositioner.ChangePosition(data, 2);
            return success
                ? Response.CreateSuccess("Window moved")
                : Response.CreateError("Error moving window");
        }
        catch (Exception ex)
        {
            Logger.Error("Exception moving window", ex);
            return Response.CreateError("Error moving window");
        }
    }

    protected override JsonTypeInfo<Position> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.Position;
    }
}
