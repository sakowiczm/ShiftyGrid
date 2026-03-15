using Windows.Win32.Foundation;
using ShiftyGrid.Common;

namespace ShiftyGrid.Windows;

/// <summary>
/// Utility methods for window geometry calculations including overlap detection,
/// adjacency checking, and spatial relationships between windows.
/// </summary>
internal static class WindowGeometry
{
    /// <summary>
    /// Calculates the vertical overlap between two rectangles.
    /// Used primarily for determining adjacency in left/right directions.
    /// </summary>
    /// <param name="rect1">First rectangle</param>
    /// <param name="rect2">Second rectangle</param>
    /// <returns>Height of the overlapping region, or 0 if no overlap</returns>
    public static int CalculateVerticalOverlap(RECT rect1, RECT rect2)
    {
        int overlapTop = Math.Max(rect1.top, rect2.top);
        int overlapBottom = Math.Min(rect1.bottom, rect2.bottom);

        if (overlapTop >= overlapBottom)
            return 0; // No overlap

        return overlapBottom - overlapTop;
    }

    /// <summary>
    /// Calculates the horizontal overlap between two rectangles.
    /// Used primarily for determining adjacency in up/down directions.
    /// </summary>
    /// <param name="rect1">First rectangle</param>
    /// <param name="rect2">Second rectangle</param>
    /// <returns>Width of the overlapping region, or 0 if no overlap</returns>
    public static int CalculateHorizontalOverlap(RECT rect1, RECT rect2)
    {
        int overlapLeft = Math.Max(rect1.left, rect2.left);
        int overlapRight = Math.Min(rect1.right, rect2.right);

        if (overlapLeft >= overlapRight)
            return 0; // No overlap

        return overlapRight - overlapLeft;
    }

    /// <summary>
    /// Checks if two rectangles have any vertical overlap.
    /// </summary>
    public static bool HasVerticalOverlap(RECT rect1, RECT rect2)
    {
        return rect1.top < rect2.bottom && rect1.bottom > rect2.top;
    }

    /// <summary>
    /// Checks if two rectangles have any horizontal overlap.
    /// </summary>
    public static bool HasHorizontalOverlap(RECT rect1, RECT rect2)
    {
        return rect1.left < rect2.right && rect1.right > rect2.left;
    }

    /// <summary>
    /// Calculates the total area of overlap between two rectangles.
    /// </summary>
    /// <param name="rect1">First rectangle</param>
    /// <param name="rect2">Second rectangle</param>
    /// <returns>Area of overlap in square pixels, or 0 if no overlap</returns>
    public static int CalculateOverlapArea(RECT rect1, RECT rect2)
    {
        int overlapLeft = Math.Max(rect1.left, rect2.left);
        int overlapTop = Math.Max(rect1.top, rect2.top);
        int overlapRight = Math.Min(rect1.right, rect2.right);
        int overlapBottom = Math.Min(rect1.bottom, rect2.bottom);

        if (overlapLeft >= overlapRight || overlapTop >= overlapBottom)
            return 0; // No overlap

        return (overlapRight - overlapLeft) * (overlapBottom - overlapTop);
    }

    /// <summary>
    /// Checks if two rectangles are adjacent (close enough to be considered neighbors).
    /// </summary>
    /// <param name="rect1">First rectangle</param>
    /// <param name="rect2">Second rectangle</param>
    /// <param name="maxGap">Maximum gap in pixels to still be considered adjacent (default: 10)</param>
    /// <returns>True if rectangles are adjacent within the gap tolerance</returns>
    public static bool AreAdjacent(RECT rect1, RECT rect2, int maxGap = 10)
    {
        // Check if horizontally adjacent (left/right)
        if (HasVerticalOverlap(rect1, rect2))
        {
            int gap = Math.Min(
                Math.Abs(rect1.right - rect2.left),
                Math.Abs(rect2.right - rect1.left)
            );
            if (gap <= maxGap)
                return true;
        }

        // Check if vertically adjacent (up/down)
        if (HasHorizontalOverlap(rect1, rect2))
        {
            int gap = Math.Min(
                Math.Abs(rect1.bottom - rect2.top),
                Math.Abs(rect2.bottom - rect1.top)
            );
            if (gap <= maxGap)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates the distance between the edges of two rectangles in a specific direction.
    /// Positive values indicate separation, negative values indicate overlap.
    /// </summary>
    public static int CalculateEdgeDistance(RECT from, RECT to, Direction direction)
    {
        return direction switch
        {
            Direction.Left => from.left - to.right,
            Direction.Right => to.left - from.right,
            Direction.Up => from.top - to.bottom,
            Direction.Down => to.top - from.bottom,
            _ => int.MaxValue
        };
    }
}
