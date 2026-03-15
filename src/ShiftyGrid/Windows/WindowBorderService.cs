using Windows.Win32.Foundation;

namespace ShiftyGrid.Windows;

/// <summary>
/// Calculates border offsets for windows to handle invisible borders/shadows.
/// Windows often have extended frames for drop shadows and borders that are not part of the visible rect.
/// </summary>
internal static class WindowBorderService
{
    /// <summary>
    /// Represents the border offsets for all four sides of a window.
    /// </summary>
    public record BorderOffsets(int Left, int Top, int Right, int Bottom);

    /// <summary>
    /// Calculates the border offsets between the visible rect and the extended rect.
    /// These offsets account for invisible window borders and shadows.
    /// </summary>
    /// <param name="window">The window to calculate offsets for</param>
    /// <returns>Border offsets for all four sides</returns>
    public static BorderOffsets CalculateOffsets(Window window)
    {
        return new BorderOffsets(
            Left: window.Rect.left - window.ExtendedRect.left,
            Top: window.Rect.top - window.ExtendedRect.top,
            Right: window.ExtendedRect.right - window.Rect.right,
            Bottom: window.ExtendedRect.bottom - window.Rect.bottom
        );
    }

    /// <summary>
    /// Calculates the width offset between extended and visible rects.
    /// This is the total horizontal border/shadow width.
    /// </summary>
    public static int CalculateWidthOffset(Window window)
    {
        return window.ExtendedRect.Width() - window.Rect.Width();
    }

    /// <summary>
    /// Calculates the height offset between extended and visible rects.
    /// This is the total vertical border/shadow height.
    /// </summary>
    public static int CalculateHeightOffset(Window window)
    {
        return window.ExtendedRect.Height() - window.Rect.Height();
    }

    /// <summary>
    /// Applies border offsets to a target visible rect to get the extended rect coordinates
    /// needed for SetWindowPos.
    /// </summary>
    /// <param name="visibleRect">The desired visible rectangle</param>
    /// <param name="offsets">The border offsets from the window</param>
    /// <returns>Extended rect coordinates for SetWindowPos</returns>
    public static (int x, int y, int width, int height) ApplyOffsetsToVisibleRect(RECT visibleRect, BorderOffsets offsets)
    {
        return (
            x: visibleRect.left - offsets.Left,
            y: visibleRect.top - offsets.Top,
            width: visibleRect.Width() + offsets.Left + offsets.Right,
            height: visibleRect.Height() + offsets.Top + offsets.Bottom
        );
    }

    /// <summary>
    /// Applies border offsets to convert a target visible position/size to extended coordinates.
    /// Use this when you want to position a window at specific visible coordinates.
    /// </summary>
    /// <param name="visibleX">Desired visible X coordinate</param>
    /// <param name="visibleY">Desired visible Y coordinate</param>
    /// <param name="visibleWidth">Desired visible width</param>
    /// <param name="visibleHeight">Desired visible height</param>
    /// <param name="offsets">The border offsets from the window</param>
    /// <returns>Extended coordinates for SetWindowPos</returns>
    public static (int x, int y, int width, int height) ApplyOffsets(int visibleX, int visibleY, int visibleWidth, int visibleHeight, BorderOffsets offsets)
    {
        return (
            x: visibleX - offsets.Left,
            y: visibleY - offsets.Top,
            width: visibleWidth + offsets.Left + offsets.Right,
            height: visibleHeight + offsets.Top + offsets.Bottom
        );
    }
}
