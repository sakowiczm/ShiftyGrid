using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Common;

namespace ShiftyGrid.Operations.Handlers;

internal class FocusCommandHandler : RequestHandler<Direction>
{
    private readonly WindowNavigationService _WindowNavigationService;
    private readonly WindowMatcher _windowMatcher;

    public FocusCommandHandler(WindowNavigationService WindowNavigationService, WindowMatcher windowMatcher)
    {
        _WindowNavigationService = WindowNavigationService ?? throw new ArgumentNullException(nameof(WindowNavigationService));
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
        var targetWindow = _WindowNavigationService.GetAdjacentWindow(activeWindow, direction);

        // If no adjacent window found, try wrap-around
        if (targetWindow == null)
        {
            targetWindow = GetWrapAroundWindow(activeWindow, direction);
        }

        if (targetWindow == null)
        {
            Logger.Warning($"FocusCommand: No window found {direction} from '{activeWindow.Text}' [{activeWindow.ClassName}]");
            return false;
        }

        // Set focus to the target window using AttachThreadInput pattern for reliability
        var success = SetForegroundWindowReliably(targetWindow.Handle);

        if (success)
        {
            Logger.Info($"FocusCommand: Focus moved {direction} to '{targetWindow.Text}' [{targetWindow.ClassName}]");
        }
        else
        {
            Logger.Warning($"FocusCommand: Failed to set foreground window '{targetWindow.Text}'");
        }

        return success;
    }

    /// <summary>
    /// Set foreground window using AttachThreadInput pattern for reliability
    /// Windows restricts SetForegroundWindow - this pattern bypasses those restrictions
    /// </summary>
    private unsafe bool SetForegroundWindowReliably(HWND hwnd)
    {
        try
        {
            var currentForeground = PInvoke.GetForegroundWindow();
            if (currentForeground == hwnd)
            {
                // Already the foreground window
                return true;
            }

            // Initialize message queue for this thread if needed.
            // Thread pool threads have no Win32 message queue by default, and AttachThreadInput
            // requires both threads to have message queues.
            PInvoke.PeekMessage(out _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);

            uint currentThreadId = PInvoke.GetCurrentThreadId();
            uint foregroundThreadId = PInvoke.GetWindowThreadProcessId(currentForeground, null);

            bool attached = false;
            if (foregroundThreadId != currentThreadId && foregroundThreadId != 0)
            {
                // Attach to foreground thread's input queue to gain foreground permission
                attached = PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, true);
                if (!attached)
                {
                    Logger.Debug($"FocusCommand: AttachThreadInput failed. Error: {Marshal.GetLastWin32Error()}");
                }
            }

            var success = PInvoke.SetForegroundWindow(hwnd);

            if (attached)
            {
                PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, false);
            }

            if (!success)
            {
                Logger.Debug($"FocusCommand: SetForegroundWindow failed. Error: {Marshal.GetLastWin32Error()}");
            }

            return success;
        }
        catch (Exception ex)
        {
            Logger.Debug($"FocusCommand: Error setting foreground window: {ex.Message}");
            return false;
        }
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
        var candidateWindows = _WindowNavigationService.GetWindowsOnMonitor(activeWindow.MonitorHandle);

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

            // Skip windows with no title (background/internal process windows)
            if (string.IsNullOrWhiteSpace(window.Text))
                continue;

            // Skip owned/popup windows (dialogs, internal UI elements like Terminal's Command Palette)
            if (!window.IsParent)
                continue;

            // Check for required overlap based on direction
            bool hasRequiredOverlap = (direction == Direction.Left || direction == Direction.Right)
                ? WindowGeometry.CalculateVerticalOverlap(activeWindow.Rect, window.Rect) > 0
                : WindowGeometry.CalculateHorizontalOverlap(activeWindow.Rect, window.Rect) > 0;

            if (!hasRequiredOverlap)
                continue;

            // Skip windows that are obscured by other windows
            if (_WindowNavigationService.IsObscuredByOtherWindows(window, candidateWindows))
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
