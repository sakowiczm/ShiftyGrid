using ShiftyGrid.Configuration;
using ShiftyGrid.Windows;

namespace ShiftyGrid.Helpers;

/// <summary>
/// Converts between pixel coordinates and grid positions
/// </summary>
internal static class GridCoordinateConverter
{
    /// <summary>
    /// Converts a window's pixel position to grid coordinates.
    /// Rounds to nearest grid point for windows not aligned to grid.
    /// Enforces minimum size of grid unit.
    /// </summary>
    /// <param name="window">The window to convert</param>
    /// <param name="grid">The grid system to use</param>
    /// <returns>Position in grid coordinates</returns>
    public static Position ConvertWindowToGridPosition(Window window, Grid grid)
    {
        var monitor = window.MonitorRect;
        var rect = window.Rect; // Use visual rect, not ExtendedRect (shadows irrelevant to grid)

        // Calculate grid cell dimensions
        double cellWidth = (monitor.right - monitor.left) / (double)grid.Columns;
        double cellHeight = (monitor.bottom - monitor.top) / (double)grid.Rows;

        // Convert pixel coordinates to grid coordinates with rounding
        int startX = (int)Math.Round((rect.left - monitor.left) / cellWidth, MidpointRounding.AwayFromZero);
        int startY = (int)Math.Round((rect.top - monitor.top) / cellHeight, MidpointRounding.AwayFromZero);
        int endX = (int)Math.Round((rect.right - monitor.left) / cellWidth, MidpointRounding.AwayFromZero);
        int endY = (int)Math.Round((rect.bottom - monitor.top) / cellHeight, MidpointRounding.AwayFromZero);

        // Clamp to grid boundaries
        startX = Math.Clamp(startX, 0, grid.Columns);
        startY = Math.Clamp(startY, 0, grid.Rows);
        endX = Math.Clamp(endX, 0, grid.Columns);
        endY = Math.Clamp(endY, 0, grid.Rows);

        // Ensure minimum size (2 grid unit)
        if (endX - startX < 1) endX = startX + 2;
        if (endY - startY < 1) endY = startY + 2;

        return new Position(grid, startX, startY, endX, endY);
    }
}
