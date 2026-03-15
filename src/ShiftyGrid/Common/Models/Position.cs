using System.Text.Json.Serialization;

namespace ShiftyGrid.Infrastructure.Models;

public record struct Position(
    [property: JsonPropertyName("grid")] Grid Grid,
    [property: JsonPropertyName("startX")] int StartX,
    [property: JsonPropertyName("startY")] int StartY,
    [property: JsonPropertyName("endX")] int EndX,
    [property: JsonPropertyName("endY")] int EndY)
{
    /// <summary>
    /// Parses position coordinates like "0,0,6,12" or "0:0:6:12" with optional grid.
    /// </summary>
    public static Position Parse(string positionString, string? gridString = null)
    {
        var grid = gridString != null ? Grid.Parse(gridString) : new Grid(12, 12);

        // Support both comma and colon separators
        var coords = positionString.Split(new[] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries);

        if (coords.Length != 4)
        {
            throw new ArgumentException(
                $"Invalid position format: {positionString}. Expected 4 coordinates: startX,startY,endX,endY");
        }

        if (!int.TryParse(coords[0], out int startX) ||
            !int.TryParse(coords[1], out int startY) ||
            !int.TryParse(coords[2], out int endX) ||
            !int.TryParse(coords[3], out int endY))
        {
            throw new ArgumentException($"Invalid coordinate values in: {positionString}. All values must be integers.");
        }

        return new Position(grid, startX, startY, endX, endY);
    }

    public void Deconstruct(out int startX, out int startY, out int endX, out int endY)
    {
        startX = StartX;
        startY = StartY;
        endX = EndX;
        endY = EndY;
    }
}
