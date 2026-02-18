using System.Text.Json.Serialization;

namespace ShiftyGrid.Configuration;

// todo: in the future we can have different grid for each monitor - would that make sense?

public record struct Grid([property: JsonPropertyName("collumns")] int Columns, [property: JsonPropertyName("rows")] int Rows);

//public record struct Position(Grid Grid, int StartX, int StartY, int EndX, int EndY)
public record struct Position(
      [property: JsonPropertyName("grid")] Grid Grid,
      [property: JsonPropertyName("startX")] int StartX,
      [property: JsonPropertyName("startY")] int StartY,
      [property: JsonPropertyName("endX")] int EndX,
      [property: JsonPropertyName("endY")] int EndY)
{
    // todo: can have keyboard shortcut associated 
    // idea: can have name as second param of move command but this has little sense as we would move active console unless other process sends this command
    
    public void Deconstruct(out int startX, out int startY, out int endX, out int endY)
    {
        startX = StartX;
        startY = StartY;
        endX = EndX;
        endY = EndY;
    }
    
    // temp for testing
    public static readonly Position LeftTop = new Position(new Grid(10, 10), 0, 0, 5, 5);
    public static readonly Position RightTop = new Position(new Grid(10, 10), 5, 0, 10, 5);
    public static readonly Position LeftBottom = new Position(new Grid(10, 10), 0, 5, 5, 10);
    public static readonly Position RightBottom = new Position(new Grid(10, 10), 5, 5, 10, 10);
    public static readonly Position LeftHalf = new Position(new Grid(10, 10), 0, 0, 5, 10);
    public static readonly Position RightHalf = new Position(new Grid(10, 10), 5, 0, 10, 10);
}
