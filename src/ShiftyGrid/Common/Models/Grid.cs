using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ShiftyGrid.Infrastructure.Models;

public record struct Grid(
    [property: JsonPropertyName("collumns")] int Columns,
    [property: JsonPropertyName("rows")] int Rows)
{
    /// <summary>
    /// Parses grid string like "12x12" or "24x24" into Grid object.
    /// </summary>
    public static Grid Parse(string gridString)
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
}
