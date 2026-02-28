using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ShiftyGrid.Handlers;

internal class PromoteCommandHandler : RequestHandler<string>
{
    protected override Response Handle(string data)
    {
        try
        {
            var result = Execute();
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

    protected override JsonTypeInfo<string> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.String;
    }

    private (bool Success, string Message) Execute()
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
            // These offsets account for invisible window borders/shadows
            int leftOffset = currentWindow.Rect.left - currentWindow.ExtendedRect.left;
            int topOffset = currentWindow.Rect.top - currentWindow.ExtendedRect.top;
            int widthOffset = (currentWindow.ExtendedRect.right - currentWindow.ExtendedRect.left) -
                              (currentWindow.Rect.right - currentWindow.Rect.left);
            int heightOffset = (currentWindow.ExtendedRect.bottom - currentWindow.ExtendedRect.top) -
                               (currentWindow.Rect.bottom - currentWindow.Rect.top);

            Logger.Debug($"Demotion border offsets: left={leftOffset}, top={topOffset}, width={widthOffset}, height={heightOffset}");

            // Convert visible coords to extended coords for SetWindowPos
            int targetX = originalRect.left - leftOffset;
            int targetY = originalRect.top - topOffset;
            int targetWidth = originalRect.Width + widthOffset;
            int targetHeight = originalRect.Height + heightOffset;

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
                        // These offsets account for invisible window borders/shadows
                        int leftOffset = prevWindow.Rect.left - prevWindow.ExtendedRect.left;
                        int topOffset = prevWindow.Rect.top - prevWindow.ExtendedRect.top;
                        int widthOffset = (prevWindow.ExtendedRect.right - prevWindow.ExtendedRect.left) -
                                          (prevWindow.Rect.right - prevWindow.Rect.left);
                        int heightOffset = (prevWindow.ExtendedRect.bottom - prevWindow.ExtendedRect.top) -
                                           (prevWindow.Rect.bottom - prevWindow.Rect.top);

                        Logger.Debug($"Auto-demotion border offsets: left={leftOffset}, top={topOffset}, width={widthOffset}, height={heightOffset}");

                        // Convert visible coords to extended coords for SetWindowPos
                        int targetX = prevRect.left - leftOffset;
                        int targetY = prevRect.top - topOffset;
                        int targetWidth = prevRect.Width + widthOffset;
                        int targetHeight = prevRect.Height + heightOffset;

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

            // PROMOTE: save current position and move to CenterWide
            Logger.Info($"Promoting window '{activeWindow.Text}' to CenterWide");

            var originalRect = activeWindow.Rect;

            // Move to CenterWide position
            var positioned = WindowPositioner.ChangePosition(activeWindow, Position.CenterWide, gap: 2);
            if (!positioned)
            {
                Logger.Warning("Failed to position window to CenterWide");
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
