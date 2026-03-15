using Windows.Win32.Foundation;

namespace ShiftyGrid.Windows;

internal static class RectExtensions
{
    public static int Width(this RECT rect) => rect.right - rect.left;

    public static int Height(this RECT rect) => rect.bottom - rect.top;

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

    public static bool Contains(this RECT rect, int x, int y)
    {
        return x >= rect.left && x < rect.right && y >= rect.top && y < rect.bottom;
    }
}
