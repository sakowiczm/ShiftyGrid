using ShiftyGrid.Server;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Handlers;

internal interface IRequestHandler
{
    Response Handle(Request request);
}

internal abstract class RequestHandler<T> : IRequestHandler
{
    public Response Handle(Request request)
    {
        try
        {
            T? data = default;

            if (request.Data.HasValue)
            {
                data = JsonSerializer.Deserialize<T>(request.Data.Value, GetJsonTypeInfo());

                if (data == null)
                {
                    return Response.CreateError($"Failed to deserialize data for {GetType().Name}");
                }
            }

            return Handle(data!);
        }
        catch (JsonException ex)
        {
            return Response.CreateError($"Invalid data format: {ex.Message}");
        }
    }

    protected abstract Response Handle(T data);
    protected abstract JsonTypeInfo<T> GetJsonTypeInfo();
}