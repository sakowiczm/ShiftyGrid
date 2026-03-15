using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Common;

namespace ShiftyGrid.Operations.Handlers;

internal class PromoteCommandHandler : RequestHandler<Position>
{
    private readonly int _gap;

    public PromoteCommandHandler(int gap)
    {
        _gap = gap;
    }

    protected override Response Handle(Position data)
    {
        try
        {
            var result = Execute(data);
            return result.Success
                ? Response.CreateSuccess(result.Message)
                : Response.CreateError(result.Message);
        }
        catch (Exception ex)
        {
            Logger.Error("Exception in promote command", ex);
            return Response.CreateError("Error toggling window promotion");
        }
    }

    protected override JsonTypeInfo<Position> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.Position;
    }

    private (bool Success, string Message) Execute(Position targetPosition)
    {
        // Get the active window
        var activeWindow = Window.GetForeground();
        if (activeWindow == null)
        {
            Logger.Debug("No active window found");
            return (false, "No active window");
        }

        if (activeWindow.IsFullscreen)
        {
            Logger.Debug("Cannot promote fullscreen window");
            return (false, "Cannot promote fullscreen window");
        }

        if (!activeWindow.IsParent)
        {
            Logger.Debug("Cannot promote child window");
            return (false, "Cannot promote child window");
        }

        var monitor = activeWindow.MonitorHandle;
        var stateManager = WindowStateManager.Instance;
        var currentPromotedState = stateManager.GetPromotedWindow(monitor);

        // Check if the current window is already promoted
        if (currentPromotedState != null && currentPromotedState.WindowHandle == activeWindow.Handle)
        {
            // DEMOTE: restore to original position
            Logger.Info($"Demoting window '{activeWindow.Text}' back to original position");

            // Validate window handle before restoration
            if (!PInvoke.IsWindow(currentPromotedState.WindowHandle))
            {
                Logger.Warning("Promoted window handle is no longer valid");
                stateManager.ClearPromotedWindow(monitor);
                return (false, "Window no longer exists");
            }

            // Restore to original position
            var originalRect = currentPromotedState.OriginalRect;

            // Get current window to calculate border offsets
            var currentWindow = Window.GetForeground();
            if (currentWindow == null)
            {
                Logger.Warning("Cannot get current window for border offset calculation");
                stateManager.ClearPromotedWindow(monitor);
                return (false, "Window no longer available");
            }

            // Calculate current window's border offsets
            // Calculate border offsets to account for invisible window borders/shadows
            int widthOffset = WindowBorderService.CalculateWidthOffset(currentWindow);
            int heightOffset = WindowBorderService.CalculateHeightOffset(currentWindow);
            var offsets = WindowBorderService.CalculateOffsets(currentWindow);

            Logger.Debug($"Demotion border offsets: left={offsets.Left}, top={offsets.Top}, width={widthOffset}, height={heightOffset}");

            // Convert visible coords to extended coords for SetWindowPos
            var (targetX, targetY, targetWidth, targetHeight) = WindowBorderService.ApplyOffsets(
                originalRect.left, originalRect.top, originalRect.Width, originalRect.Height, offsets);

            var success = PInvoke.SetWindowPos(
                currentPromotedState.WindowHandle,
                HWND.Null,
                targetX,
                targetY,
                targetWidth,
                targetHeight,
                SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
            );

            if (!success)
            {
                Logger.Warning("Failed to restore window position");
            }

            // Clear the promoted state
            stateManager.ClearPromotedWindow(monitor);

            Logger.Debug("Window demoted successfully");
            return (true, "Window demoted");
        }
        else
        {
            // If a different window is promoted on this monitor, auto-demote it first
            if (currentPromotedState != null)
            {
                Logger.Info("Auto-demoting previously promoted window on this monitor");

                // Best effort demotion - don't fail if it doesn't work
                if (PInvoke.IsWindow(currentPromotedState.WindowHandle))
                {
                    var prevRect = currentPromotedState.OriginalRect;

                    // Get the previous promoted window to calculate ITS border offsets
                    var prevWindow = Window.FromHandle(currentPromotedState.WindowHandle);
                    if (prevWindow != null)
                    {
                        // Calculate border offsets for the previous window
                        int widthOffset = WindowBorderService.CalculateWidthOffset(prevWindow);
                        int heightOffset = WindowBorderService.CalculateHeightOffset(prevWindow);
                        var offsets = WindowBorderService.CalculateOffsets(prevWindow);

                        Logger.Debug($"Auto-demotion border offsets: left={offsets.Left}, top={offsets.Top}, width={widthOffset}, height={heightOffset}");

                        // Convert visible coords to extended coords for SetWindowPos
                        var (targetX, targetY, targetWidth, targetHeight) = WindowBorderService.ApplyOffsets(
                            prevRect.left, prevRect.top, prevRect.Width, prevRect.Height, offsets);

                        PInvoke.SetWindowPos(
                            currentPromotedState.WindowHandle,
                            HWND.Null,
                            targetX,
                            targetY,
                            targetWidth,
                            targetHeight,
                            SET_WINDOW_POS_FLAGS.SWP_NOZORDER |
                            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
                        );
                    }
                    else
                    {
                        Logger.Debug("Previous promoted window no longer available for border offset calculation");
                    }
                }
                else
                {
                    Logger.Debug("Previously promoted window no longer exists");
                }

                stateManager.ClearPromotedWindow(monitor);
            }

            // PROMOTE: save current position and move to target position
            Logger.Info($"Promoting window '{activeWindow.Text}' to position: {targetPosition}");

            var originalRect = activeWindow.Rect;

            // Move to target position
            var positioned = WindowPositioner.ChangePosition(activeWindow, targetPosition, _gap);
            if (!positioned)
            {
                Logger.Warning($"Failed to position window to {targetPosition}");
                return (false, "Failed to promote window");
            }

            // Store the promoted state
            var newState = new PromotedWindowState
            {
                WindowHandle = activeWindow.Handle,
                OriginalRect = originalRect,
                MonitorHandle = monitor,
                PromotedAt = DateTime.UtcNow
            };

            stateManager.SetPromotedWindow(monitor, newState);

            Logger.Debug("Window promoted successfully");
            return (true, "Window promoted");
        }
    }
}
