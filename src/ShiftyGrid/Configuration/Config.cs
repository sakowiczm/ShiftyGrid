using System.Text.Json.Serialization;

namespace ShiftyGrid.Configuration;

public static class Config
{
    /// <summary>
    /// The gap in pixels between windows for visual separation.
    /// </summary>
    public const int GAP = 4;

    public const int PROXIMITY_THRESHOLD = 20;
}

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


// Each window should have at most ONE matching rule. Having multiple matchers
// for the same window with different positions would be incorrect configuration.
public record WindowMatch
{
    [JsonPropertyName("titlePattern")]
    public string? TitlePattern { get; init; }

    [JsonPropertyName("className")]
    public string? ClassName { get; init; }

    [JsonPropertyName("processName")]
    public string? ProcessName { get; init; }

    [JsonPropertyName("position")]
    public required Position Position { get; init; }
}

public class OrganizeConfig
{
    [JsonPropertyName("matchers")]
    public List<WindowMatch> Matchers { get; init; } = new();

    public static OrganizeConfig GetDefault()
    {
        return new OrganizeConfig
        {
            Matchers = new List<WindowMatch>
            {
                new() { TitlePattern = "Slack", Position = Position.RightHalf },
                new() { ProcessName = "WindowsTerminal", Position = Position.LeftHalf },
                new() { TitlePattern = "Docker Desktop", Position = Position.RightHalf },
                new() { ProcessName = "Fork", Position = Position.LeftHalf },
                new() { ProcessName = "Code", Position = Position.LeftHalf }
            }
        };
    }
}
