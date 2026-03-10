using System.Text.RegularExpressions;

namespace ShiftyGrid.Configuration;

public static class GridParser
{
    /// <summary>
    /// Parses grid string like "12x12" or "24x24" into Grid object.
    /// </summary>
    public static Grid ParseGrid(string gridString)
    {
        var match = Regex.Match(gridString, @"^(\d+)x(\d+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            throw new ArgumentException(
                $"Invalid grid format: {gridString}. Expected format: NxM (e.g., 12x12, 24x24)");
        }

        var rows = int.Parse(match.Groups[1].Value);
        var cols = int.Parse(match.Groups[2].Value);
        return new Grid(rows, cols);
    }

    /// <summary>
    /// Parses position coordinates like "0,0,6,12" or "0:0:6:12" with optional grid.
    /// </summary>
    public static Position ParsePosition(string positionString, string? gridString = null)
    {
        var grid = gridString != null ? ParseGrid(gridString) : new Grid(12, 12);

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
}
