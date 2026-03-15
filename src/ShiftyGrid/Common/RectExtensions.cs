using Windows.Win32.Foundation;

namespace ShiftyGrid.Common;

/// <summary>
/// Extension methods for RECT struct to simplify common calculations.
/// </summary>
internal static class RectExtensions
{
    /// <summary>
    /// Gets the width of the rectangle.
    /// </summary>
    public static int Width(this RECT rect) => rect.right - rect.left;

    /// <summary>
    /// Gets the height of the rectangle.
    /// </summary>
    public static int Height(this RECT rect) => rect.bottom - rect.top;

    /// <summary>
    /// Gets the area of the rectangle.
    /// </summary>
    public static int Area(this RECT rect) => rect.Width() * rect.Height();

    /// <summary>
    /// Creates a RECT from X, Y coordinates and width/height dimensions.
    /// </summary>
    public static RECT FromXYWH(int x, int y, int width, int height)
    {
        return new RECT
        {
            left = x,
            top = y,
            right = x + width,
            bottom = y + height
        };
    }

    /// <summary>
    /// Checks if the rectangle contains the specified point.
    /// </summary>
    public static bool Contains(this RECT rect, int x, int y)
    {
        return x >= rect.left && x < rect.right && y >= rect.top && y < rect.bottom;
    }
}
