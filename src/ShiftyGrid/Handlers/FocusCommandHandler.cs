using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ShiftyGrid.Handlers;

internal class FocusCommandHandler : RequestHandler<Direction>
{
    private readonly WindowNeighborHelper _windowNeighborHelper;
    private readonly WindowMatcher _windowMatcher;

    public FocusCommandHandler(WindowNeighborHelper windowNeighborHelper, WindowMatcher windowMatcher)
    {
        _windowNeighborHelper = windowNeighborHelper ?? throw new ArgumentNullException(nameof(windowNeighborHelper));
        _windowMatcher = windowMatcher ?? throw new ArgumentNullException(nameof(windowMatcher));
    }

    protected override Response Handle(Direction direction)
    {
        try
        {
            var success = Execute(direction);
            return success
                ? Response.CreateSuccess($"Focus moved {direction}")
                : Response.CreateError($"Could not move focus {direction}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Exception moving focus {direction}", ex);
            return Response.CreateError($"Error moving focus {direction}");
        }
    }

    protected override JsonTypeInfo<Direction> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.Direction;
    }

    private bool Execute(Direction direction)
    {
        var activeWindow = Window.GetForeground();
        if (activeWindow == null)
        {
            Logger.Debug("FocusCommand: No active window");
            return false;
        }

        // First try direct adjacency using existing helper
        var targetWindow = _windowNeighborHelper.GetAdjacentWindow(activeWindow, direction);

        // If no adjacent window found, try wrap-around
        if (targetWindow == null)
        {
            targetWindow = GetWrapAroundWindow(activeWindow, direction);
        }

        if (targetWindow == null)
        {
            Logger.Debug($"FocusCommand: No window found in direction {direction} (with wrap-around)");
            return false;
        }

        // Set focus to the target window
        var success = PInvoke.SetForegroundWindow(targetWindow.Handle);

        if (success)
        {
            Logger.Info($"FocusCommand: Focus moved {direction} to '{targetWindow.Text}'");
        }
        else
        {
            Logger.Warning($"FocusCommand: Failed to set foreground window '{targetWindow.Text}'");
        }

        return success;
    }

    /// <summary>
    /// Finds a window for wrap-around navigation when no adjacent window exists.
    /// For Left: finds rightmost window with vertical overlap
    /// For Right: finds leftmost window with vertical overlap
    /// For Up: finds bottommost window with horizontal overlap
    /// For Down: finds topmost window with horizontal overlap
    /// Filters out windows that are obscured by other windows.
    /// </summary>
    private Window? GetWrapAroundWindow(Window activeWindow, Direction direction)
    {
        var candidateWindows = _windowNeighborHelper.GetWindowsOnMonitor(activeWindow.MonitorHandle);

        Window? bestMatch = null;
        int bestEdgePosition = direction switch
        {
            Direction.Left => int.MinValue,  // Looking for rightmost (highest right value)
            Direction.Right => int.MaxValue, // Looking for leftmost (lowest left value)
            Direction.Up => int.MinValue,    // Looking for bottommost (highest bottom value)
            Direction.Down => int.MaxValue,  // Looking for topmost (lowest top value)
            _ => 0
        };

        foreach (var window in candidateWindows)
        {
            // Skip the active window itself
            if (window.Handle == activeWindow.Handle)
                continue;

            // Skip border overlay windows
            if (_windowMatcher.ShouldIgnore(window))
                continue;

            // Check for required overlap based on direction
            bool hasRequiredOverlap = (direction == Direction.Left || direction == Direction.Right)
                ? WindowGeometry.CalculateVerticalOverlap(activeWindow.Rect, window.Rect) > 0
                : WindowGeometry.CalculateHorizontalOverlap(activeWindow.Rect, window.Rect) > 0;

            if (!hasRequiredOverlap)
                continue;

            // Skip windows that are obscured by other windows
            if (_windowNeighborHelper.IsObscuredByOtherWindows(window, candidateWindows))
            {
                Logger.Debug($"FocusCommand: Skipping '{window.Text}' (z-order {window.ZOrder}) - obscured in wrap-around");
                continue;
            }

            // Find the window at the opposite edge
            bool isBetterMatch = direction switch
            {
                Direction.Left => window.Rect.right > bestEdgePosition,  // Rightmost
                Direction.Right => window.Rect.left < bestEdgePosition,  // Leftmost
                Direction.Up => window.Rect.bottom > bestEdgePosition,   // Bottommost
                Direction.Down => window.Rect.top < bestEdgePosition,    // Topmost
                _ => false
            };

            if (isBetterMatch)
            {
                bestMatch = window;
                bestEdgePosition = direction switch
                {
                    Direction.Left => window.Rect.right,
                    Direction.Right => window.Rect.left,
                    Direction.Up => window.Rect.bottom,
                    Direction.Down => window.Rect.top,
                    _ => bestEdgePosition
                };
            }
        }

        if (bestMatch != null)
        {
            Logger.Debug($"FocusCommand: Wrap-around found '{bestMatch.Text}' in direction {direction}");
        }

        return bestMatch;
    }

    // Note: Overlap calculation methods moved to WindowGeometry class
}
