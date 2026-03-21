using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Common;

namespace ShiftyGrid.Operations.Handlers;

public record ArrangeOptions(
    [property: JsonPropertyName("rows")] int Rows,
    [property: JsonPropertyName("cols")] int Cols,
    [property: JsonPropertyName("zone")] string? Zone = null
);

internal class ArrangeCommandHandler : RequestHandler<ArrangeOptions>
{
    private readonly WindowSelector _windowSelector;
    private readonly int _gap;

    public ArrangeCommandHandler(WindowSelector windowSelector, int gap)
    {
        _windowSelector = windowSelector ?? throw new ArgumentNullException(nameof(windowSelector));
        _gap = gap;
    }

    protected override Response Handle(ArrangeOptions options)
    {
        try
        {
            // Validate parameters
            if (options.Rows < 1 || options.Rows > 2)
            {
                return Response.CreateError($"Invalid rows: {options.Rows}. Must be between 1 and 2.");
            }

            if (options.Cols < 1 || options.Cols > 4)
            {
                return Response.CreateError($"Invalid cols: {options.Cols}. Must be between 1 and 4.");
            }

            int totalCells = options.Rows * options.Cols;
            if (totalCells > 8)
            {
                return Response.CreateError($"Invalid grid: {options.Rows}x{options.Cols} = {totalCells} cells. Maximum is 8 cells.");
            }

            if (options.Zone != null)
            {
                var parts = options.Zone.Split(',');
                if (parts.Length != 4 ||
                    !int.TryParse(parts[0], out int zx1) || !int.TryParse(parts[1], out int zy1) ||
                    !int.TryParse(parts[2], out int zx2) || !int.TryParse(parts[3], out int zy2))
                {
                    return Response.CreateError("Invalid zone format. Expected x1,y1,x2,y2 (e.g. 0,0,6,12).");
                }
                if (zx1 < 0 || zy1 < 0 || zx2 > 12 || zy2 > 12)
                {
                    return Response.CreateError("Zone coordinates must be within 0–12.");
                }
                if (zx2 <= zx1 || zy2 <= zy1)
                {
                    return Response.CreateError("Zone x2 must be greater than x1, and y2 must be greater than y1.");
                }
            }

            var success = Execute(options.Rows, options.Cols, options.Zone);
            return success
                ? Response.CreateSuccess($"Windows arranged in {options.Rows}x{options.Cols} grid")
                : Response.CreateError("Error arranging windows");
        }
        catch (Exception ex)
        {
            Logger.Error("Exception arranging windows", ex);
            return Response.CreateError("Error arranging windows");
        }
    }

    protected override JsonTypeInfo<ArrangeOptions> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.ArrangeOptions;
    }

    private bool Execute(int rows, int cols, string? zone = null)
    {
        // Get active window
        var activeWindow = Window.GetForeground();
        if (activeWindow == null)
        {
            Logger.Debug("No active window found");
            return false;
        }

        if (activeWindow.IsFullscreen)
        {
            Logger.Debug("Cannot arrange maximized/fullscreen window");
            return false;
        }

        // Calculate target window count
        int targetCount = rows * cols;

        // Select windows using 5-priority selection
        var windows = _windowSelector.SelectWindowsForArrange(activeWindow, targetCount);

        Logger.Debug($"Selected {windows.Count} windows for {rows}x{cols} grid");

        // Handle edge cases
        if (windows.Count == 0)
        {
            Logger.Debug("No windows to arrange");
            return false;
        }

        var coordinates = GenerateGridCoordinates(rows, cols, zone);

        // Assign windows to coordinates (up to available windows)
        bool success = true;
        for (int i = 0; i < Math.Min(windows.Count, coordinates.Count); i++)
        {
            var window = windows[i];

            Logger.Debug($"Arranging window '{window.Text}' to grid cell {i + 1} (row {i / cols + 1}, col {i % cols + 1})");

            success &= WindowPositioner.ChangePosition(window, coordinates[i], _gap);
        }

        return success;
    }

    private List<Coordinates> GenerateGridCoordinates(int rows, int cols, string? zone = null)
    {
        const int gridSize = 12;

        int zoneStartX = 0, zoneStartY = 0, zoneEndX = gridSize, zoneEndY = gridSize;
        if (zone != null)
        {
            var parts = zone.Split(',');
            zoneStartX = int.Parse(parts[0]);
            zoneStartY = int.Parse(parts[1]);
            zoneEndX = int.Parse(parts[2]);
            zoneEndY = int.Parse(parts[3]);
        }

        int zoneWidth = zoneEndX - zoneStartX;
        int zoneHeight = zoneEndY - zoneStartY;
        int cellWidth = zoneWidth / cols;
        int cellHeight = zoneHeight / rows;

        var positions = new List<Coordinates>();

        // Generate coordinates in reading order (left-to-right, top-to-bottom)
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int startX = zoneStartX + col * cellWidth;
                int startY = zoneStartY + row * cellHeight;
                int endX = zoneStartX + (col + 1) * cellWidth;
                int endY = zoneStartY + (row + 1) * cellHeight;

                positions.Add(new Coordinates(new Grid(gridSize, gridSize), startX, startY, endX, endY));
            }
        }

        return positions;
    }
}
