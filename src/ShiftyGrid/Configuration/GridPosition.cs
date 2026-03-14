using System.Text.Json.Serialization;

namespace ShiftyGrid.Configuration;

public record struct Grid(
    [property: JsonPropertyName("collumns")] int Columns,
    [property: JsonPropertyName("rows")] int Rows);

public record struct Position(
    [property: JsonPropertyName("grid")] Grid Grid,
    [property: JsonPropertyName("startX")] int StartX,
    [property: JsonPropertyName("startY")] int StartY,
    [property: JsonPropertyName("endX")] int EndX,
    [property: JsonPropertyName("endY")] int EndY)
{
    public void Deconstruct(out int startX, out int startY, out int endX, out int endY)
    {
        startX = StartX;
        startY = StartY;
        endX = EndX;
        endY = EndY;
    }
}
