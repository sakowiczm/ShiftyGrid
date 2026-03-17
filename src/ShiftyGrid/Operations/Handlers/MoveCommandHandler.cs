using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Common;

namespace ShiftyGrid.Operations.Handlers;

internal class MoveCommandHandler : RequestHandler<Coordinates>
{
    private readonly int _gap;

    public MoveCommandHandler(int gap)
    {
        _gap = gap;
    }

    protected override Response Handle(Coordinates data)
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

    protected override JsonTypeInfo<Coordinates> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.Coordinates;
    }
}
