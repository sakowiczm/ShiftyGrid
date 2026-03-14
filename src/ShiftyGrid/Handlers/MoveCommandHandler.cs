using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Handlers;

internal class MoveCommandHandler : RequestHandler<Position>
{
    private readonly int _gap;

    public MoveCommandHandler(int gap)
    {
        _gap = gap;
    }

    protected override Response Handle(Position data)
    {
        try
        {
            var success = WindowPositioner.ChangePosition(data, _gap);
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
