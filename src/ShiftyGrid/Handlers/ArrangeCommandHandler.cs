using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Handlers;

public record ArrangeOptions(
    [property: JsonPropertyName("rows")] int Rows,
    [property: JsonPropertyName("cols")] int Cols
);

internal class ArrangeCommandHandler : RequestHandler<ArrangeOptions>
{
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

            var success = Execute(options.Rows, options.Cols);
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

    private bool Execute(int rows, int cols)
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
        var windows = WindowSelector.SelectWindowsForArrange(activeWindow, targetCount);

        Logger.Debug($"Selected {windows.Count} windows for {rows}x{cols} grid");

        // Handle edge cases
        if (windows.Count == 0)
        {
            Logger.Debug("No windows to arrange");
            return false;
        }

        // Generate grid positions
        var positions = GenerateGridPositions(rows, cols);

        // Assign windows to positions (up to available windows)
        bool success = true;
        for (int i = 0; i < Math.Min(windows.Count, positions.Count); i++)
        {
            var window = windows[i];
            var position = positions[i];

            Logger.Debug($"Arranging window '{window.Text}' to grid cell {i + 1} (row {i / cols + 1}, col {i % cols + 1})");

            success &= WindowPositioner.ChangePosition(window, position, gap: 2);
        }

        return success;
    }

    private List<Position> GenerateGridPositions(int rows, int cols)
    {
        const int gridSize = 12;
        var positions = new List<Position>();

        int cellWidth = gridSize / cols;
        int cellHeight = gridSize / rows;

        // Generate positions in reading order (left-to-right, top-to-bottom)
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int startX = col * cellWidth;
                int startY = row * cellHeight;
                int endX = (col + 1) * cellWidth;
                int endY = (row + 1) * cellHeight;

                var position = new Position(
                    new Grid(gridSize, gridSize),
                    startX,
                    startY,
                    endX,
                    endY
                );

                positions.Add(position);
            }
        }

        return positions;
    }
}
