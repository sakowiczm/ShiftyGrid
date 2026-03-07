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
    public static readonly Position LeftTop = new Position(new Grid(12, 12), 0, 0, 6, 6);
    public static readonly Position RightTop = new Position(new Grid(12, 12), 6, 0, 12, 6);
    public static readonly Position LeftBottom = new Position(new Grid(12, 12), 0, 6, 6, 12);
    public static readonly Position RightBottom = new Position(new Grid(12, 12), 6, 6, 12, 12);

    public static readonly Position LeftHalf = new Position(new Grid(12, 12), 0, 0, 6, 12);
    public static readonly Position RightHalf = new Position(new Grid(12, 12), 6, 0, 12, 12);

    public static readonly Position Center = new Position(new Grid(12, 12), 2, 0, 10, 12);
    public static readonly Position CenterWide = new Position(new Grid(12, 12), 1, 0, 11, 12);
    public static readonly Position Full = new Position(new Grid(12, 12), 0, 0, 12, 12);

    public static readonly Position ThreeColumnsCol1 = new Position(new Grid(12, 12), 0, 0, 4, 12);
    public static readonly Position ThreeColumnsCol2 = new Position(new Grid(12, 12), 4, 0, 8, 12);
    public static readonly Position ThreeColumnsCol3 = new Position(new Grid(12, 12), 8, 0, 12, 12);
}
