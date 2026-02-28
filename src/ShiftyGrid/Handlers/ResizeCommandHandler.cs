using ShiftyGrid.Common;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace ShiftyGrid.Handlers;


// WIP

// todo: we need separate resize operation when there is no neighbour

/*
Want to focus on unifying behaviour of below methods.
Those methods would be executed in following scenarios(A|B - we have two windows both take half of the screen)
- Window A is focused and has neighbour B - Alt + Right - would execute ExpandRightEdge.
- Window B is focused and has neighbour A - Alt+Left - would execute ExpandLeftEdge.
- Window B is focused - and has neighbour A - we use Alt+Right - would execute ShrinkRight
- Window A is focused - and has neighbour B - we use Alt+Left - would execute ShrinkLeft

In both scenarios when focused window expands and neighbour shrinks.I want to:
- that both windows stays fully visible on the screen with aesthetic border around each window.Windows cannot overlap or go beyond monitor edges.
- The window that is shrinking should not get smaller than minimal width (right now 200px) - if expand of active window would result in shrinking window get's 200px or smaller - resize operation should not happen (we reached the limit). This behaviour should be identical for both edges of the screen.
*/

internal enum WindowResize
{
    // Expansion operations (context-aware Alt+Arrow)
    ExpandLeft,
    ExpandRight,
    ExpandUp,
    ExpandDown,

    // Shrink operations (explicit Shift+Alt+Arrow)
    ShrinkLeft,    // Shrink left edge (move right), right neighbor expands
    ShrinkRight,   // Shrink right edge (move left), left neighbor expands
    ShrinkUp,      // Shrink top edge (move down), bottom neighbor expands
    ShrinkDown     // Shrink bottom edge (move up), top neighbor expands
}

internal class ResizeCommandHandler : RequestHandler<WindowResize>
{
    private const int STEP_SIZE = 20;
    private const int AESTHETIC_GAP = 2; // todo: MoveCommandHandler gap - unify
    private const int MIN_WINDOW_WIDTH = 200;
    private const int MIN_WINDOW_HEIGHT = 200;

    protected override Response Handle(WindowResize direction)
    {
        try
        {
            var success = Execute(direction);
            return success
                ? Response.CreateSuccess("Window resized")
                : Response.CreateError("Error resizing window");
        }
        catch (Exception ex)
        {
            Logger.Error("Exception resizing window", ex);
            return Response.CreateError("Error resizing window");
        }
    }

    protected override JsonTypeInfo<WindowResize> GetJsonTypeInfo()
    {
        return IpcJsonContext.Default.WindowResize;
    }

    public bool Execute(WindowResize resizeStep)
    {
        var activeWindow = Window.GetForeground();
        if (activeWindow == null)
        {
            Logger.Debug("No active window found");
            return false;
        }

        if (activeWindow.IsFullscreen)
        {
            Logger.Debug("Cannot resize maximized/fullscreen window");
            return false;
        }

        return resizeStep switch
        {
            // Context-aware operations (Alt+Arrow)
            WindowResize.ExpandLeft => ResizeLeft(activeWindow),
            WindowResize.ExpandRight => ResizeRight(activeWindow),
            WindowResize.ExpandUp => ResizeUp(activeWindow),
            WindowResize.ExpandDown => ResizeDown(activeWindow),

            // Explicit shrink operations (Shift+Alt+Arrow)
            WindowResize.ShrinkLeft => ShrinkLeft(activeWindow),
            WindowResize.ShrinkRight => ShrinkRight(activeWindow),
            WindowResize.ShrinkUp => ShrinkUp(activeWindow),
            WindowResize.ShrinkDown => ShrinkDown(activeWindow),

            _ => false
        };
    }

    private bool ResizeLeft(Window focusedWindow)
    {
        // Context-aware: Expand left if left neighbor exists, else shrink opposite edge or expand to edge
        var leftNeighbor = WindowNeighborHelper.GetAdjacentWindow(focusedWindow, Direction.Left);
        Logger.Debug($"ResizeLeft: neighbor detected = {leftNeighbor != null}, neighbor = {leftNeighbor?.Text ?? "none"}");

        if (leftNeighbor != null)
        {
            // Has left neighbor: EXPAND left (shrink neighbor)
            return ExpandLeftEdge(focusedWindow, leftNeighbor);
        }
        else
        {
            // No left neighbor: Check screen edge
            if (!IsAtScreenEdge(focusedWindow, Direction.Left))
            {
                // Not at edge yet: EXPAND to edge
                return ExpandToScreenEdge(focusedWindow, Direction.Left);
            }
            else
            {
                // At edge: SHRINK opposite (right) edge instead
                return ShrinkRight(focusedWindow);
            }
        }
    }

    private bool ResizeRight(Window focusedWindow)
    {
        // Context-aware: Expand right if right neighbor exists, else shrink opposite edge or expand to edge
        var rightNeighbor = WindowNeighborHelper.GetAdjacentWindow(focusedWindow, Direction.Right);
        Logger.Debug($"ResizeRight: neighbor detected = {rightNeighbor != null}, neighbor = {rightNeighbor?.Text ?? "none"}");

        if (rightNeighbor != null)
        {
            // Has right neighbor: EXPAND right (shrink neighbor)
            return ExpandRightEdge(focusedWindow, rightNeighbor);
        }
        else
        {
            // No right neighbor: Check screen edge
            if (!IsAtScreenEdge(focusedWindow, Direction.Right))
            {
                // Not at edge yet: EXPAND to edge
                return ExpandToScreenEdge(focusedWindow, Direction.Right);
            }
            else
            {
                // At edge: SHRINK opposite (left) edge instead
                return ShrinkLeft(focusedWindow);
            }
        }
    }

    private bool ResizeUp(Window focusedWindow)
    {
        // Context-aware: Expand up if top neighbor exists, else shrink opposite edge or expand to edge
        var topNeighbor = WindowNeighborHelper.GetAdjacentWindow(focusedWindow, Direction.Up);

        if (topNeighbor != null)
        {
            // Has top neighbor: EXPAND up (shrink neighbor)
            return ExpandUpEdge(focusedWindow, topNeighbor);
        }
        else
        {
            // No top neighbor: Check screen edge
            if (!IsAtScreenEdge(focusedWindow, Direction.Up))
            {
                // Not at edge yet: EXPAND to edge
                return ExpandToScreenEdge(focusedWindow, Direction.Up);
            }
            else
            {
                // At edge: SHRINK opposite (bottom) edge instead
                return ShrinkDown(focusedWindow);
            }
        }
    }

    private bool ResizeDown(Window focusedWindow)
    {
        // Context-aware: Expand down if bottom neighbor exists, else shrink opposite edge or expand to edge
        var bottomNeighbor = WindowNeighborHelper.GetAdjacentWindow(focusedWindow, Direction.Down);

        if (bottomNeighbor != null)
        {
            // Has bottom neighbor: EXPAND down (shrink neighbor)
            return ExpandDownEdge(focusedWindow, bottomNeighbor);
        }
        else
        {
            // No bottom neighbor: Check screen edge
            if (!IsAtScreenEdge(focusedWindow, Direction.Down))
            {
                // Not at edge yet: EXPAND to edge
                return ExpandToScreenEdge(focusedWindow, Direction.Down);
            }
            else
            {
                // At edge: SHRINK opposite (top) edge instead
                return ShrinkUp(focusedWindow);
            }
        }
    }

    private bool ShrinkLeft(Window focusedWindow)
    {
        // Shrinking left edge means moving it RIGHT (inward)
        // The LEFT neighbor should expand RIGHT to fill the gap

        // Calculate new position (move left edge right)
        int newLeft = focusedWindow.Rect.left + STEP_SIZE;
        int currentWidth = focusedWindow.Rect.right - focusedWindow.Rect.left;
        int newWidth = currentWidth - STEP_SIZE;

        // Check minimum size constraint
        if (newWidth < MIN_WINDOW_WIDTH)
        {
            Logger.Debug($"Cannot shrink below minimum width ({MIN_WINDOW_WIDTH}px)");
            return false;
        }

        // Validate right boundary - ensure shrinking left edge doesn't push right edge beyond screen
        var workArea = focusedWindow.MonitorRect;
        int newRight = newLeft + newWidth;
        if (newRight > workArea.right)
        {
            Logger.Debug("Cannot shrink left edge - would push right edge beyond screen boundary");
            return false;
        }

        // Try tight neighbor first, then extended search
        var leftNeighbor = WindowNeighborHelper.GetAdjacentWindow(focusedWindow, Direction.Left);
        if (leftNeighbor == null)
        {
            leftNeighbor = WindowNeighborHelper.FindWindowForGapClosing(focusedWindow, Direction.Left);
        }

        // Calculate neighbor expansion BEFORE positioning focused window (using COMPUTED newLeft, not stale data)
        int expansionAmount = 0;
        int newNeighborWidth = 0;
        if (leftNeighbor != null)
        {
            int currentGap = newLeft - leftNeighbor.Rect.right;
            int targetGap = AESTHETIC_GAP;
            int desiredNeighborRight = newLeft - targetGap;
            expansionAmount = desiredNeighborRight - leftNeighbor.Rect.right;

            if (expansionAmount > 0)
            {
                newNeighborWidth = (leftNeighbor.Rect.right - leftNeighbor.Rect.left) + expansionAmount;
                int newNeighborRight = leftNeighbor.Rect.left + newNeighborWidth;

                // Validate both left edge and new right edge stay within bounds
                if (leftNeighbor.Rect.left < workArea.left || newNeighborRight > newLeft)
                {
                    Logger.Debug("Gap-closing skipped: would exceed screen bounds");
                    leftNeighbor = null; // Skip gap-closing but continue with focused window shrink
                }
            }
            else if (expansionAmount < 0)
            {
                Logger.Debug($"Gap ({currentGap}px) already smaller than target ({targetGap}px), no adjustment needed");
                leftNeighbor = null; // No expansion needed
            }
        }

        // NOW position focused window
        if (!PositionWindow(focusedWindow, newLeft, focusedWindow.Rect.top,
            newWidth, focusedWindow.Rect.bottom - focusedWindow.Rect.top))
            return false;

        // THEN position neighbor if validated
        if (leftNeighbor != null && expansionAmount > 0)
        {
            Logger.Debug($"Gap-closing: expanding left neighbor by {expansionAmount}px to achieve {AESTHETIC_GAP}px gap");
            PositionWindow(leftNeighbor, leftNeighbor.Rect.left, leftNeighbor.Rect.top,
                newNeighborWidth, leftNeighbor.Rect.bottom - leftNeighbor.Rect.top);
        }

        return true;
    }

    private bool ShrinkRight(Window focusedWindow)
    {
        // Shrinking right edge means moving it LEFT (inward)
        // The RIGHT neighbor should expand LEFT to fill the gap

        // Calculate new width (shrink from right edge)
        int currentWidth = focusedWindow.Rect.right - focusedWindow.Rect.left;
        int newWidth = currentWidth - STEP_SIZE;

        // Check minimum size constraint
        if (newWidth < MIN_WINDOW_WIDTH)
        {
            Logger.Debug($"Cannot shrink below minimum width ({MIN_WINDOW_WIDTH}px)");
            return false;
        }

        // Use COMPUTED new right position, not stale focusedWindow.Rect
        int newFocusedRight = focusedWindow.Rect.left + newWidth;

        // Try to find tight neighbor first (within PROXIMITY_THRESHOLD = 20px)
        var rightNeighbor = WindowNeighborHelper.GetAdjacentWindow(focusedWindow, Direction.Right);

        // If no tight neighbor found, search for windows with larger gaps
        if (rightNeighbor == null)
        {
            rightNeighbor = WindowNeighborHelper.FindWindowForGapClosing(focusedWindow, Direction.Right);
        }

        // Calculate neighbor expansion BEFORE positioning focused window (using COMPUTED newFocusedRight)
        int expansionAmount = 0;
        int newNeighborLeft = 0;
        int newNeighborWidth = 0;
        if (rightNeighbor != null)
        {
            int currentGap = rightNeighbor.Rect.left - newFocusedRight;
            int targetGap = AESTHETIC_GAP;
            int desiredNeighborLeft = newFocusedRight + targetGap;
            expansionAmount = rightNeighbor.Rect.left - desiredNeighborLeft;

            if (expansionAmount > 0)
            {
                newNeighborLeft = desiredNeighborLeft;
                newNeighborWidth = (rightNeighbor.Rect.right - rightNeighbor.Rect.left) + expansionAmount;
                int newNeighborRight = newNeighborLeft + newNeighborWidth;

                // Validate both edges
                var workArea = focusedWindow.MonitorRect;
                if (newNeighborLeft < workArea.left || newNeighborRight > workArea.right)
                {
                    Logger.Debug("Gap-closing skipped: would exceed screen bounds");
                    rightNeighbor = null; // Skip gap-closing
                }
            }
            else if (expansionAmount < 0)
            {
                Logger.Debug($"Gap ({currentGap}px) already smaller than target ({targetGap}px), no adjustment needed");
                rightNeighbor = null; // No expansion needed
            }
        }

        // Position focused window first
        if (!PositionWindow(focusedWindow, focusedWindow.Rect.left, focusedWindow.Rect.top,
            newWidth, focusedWindow.Rect.bottom - focusedWindow.Rect.top))
            return false;

        // Then neighbor if validated
        if (rightNeighbor != null && expansionAmount > 0)
        {
            Logger.Debug($"Gap-closing: expanding right neighbor by {expansionAmount}px to achieve {AESTHETIC_GAP}px gap");
            PositionWindow(rightNeighbor, newNeighborLeft, rightNeighbor.Rect.top,
                newNeighborWidth, rightNeighbor.Rect.bottom - rightNeighbor.Rect.top);
        }

        return true;
    }

    private bool ShrinkUp(Window focusedWindow)
    {
        // Shrinking top edge means moving it DOWN (inward)
        // The TOP neighbor should expand DOWN to fill the gap

        int newTop = focusedWindow.Rect.top + STEP_SIZE;
        int currentHeight = focusedWindow.Rect.bottom - focusedWindow.Rect.top;
        int newHeight = currentHeight - STEP_SIZE;

        if (newHeight < MIN_WINDOW_HEIGHT)
        {
            Logger.Debug($"Cannot shrink below minimum height ({MIN_WINDOW_HEIGHT}px)");
            return false;
        }

        var topNeighbor = WindowNeighborHelper.GetAdjacentWindow(focusedWindow, Direction.Up);
        if (topNeighbor == null)
        {
            topNeighbor = WindowNeighborHelper.FindWindowForGapClosing(focusedWindow, Direction.Up);
        }

        // Calculate neighbor expansion BEFORE positioning focused window (using COMPUTED newTop)
        int expansionAmount = 0;
        int newNeighborHeight = 0;
        if (topNeighbor != null)
        {
            int currentGap = newTop - topNeighbor.Rect.bottom;
            int targetGap = AESTHETIC_GAP;
            int desiredNeighborBottom = newTop - targetGap;
            expansionAmount = desiredNeighborBottom - topNeighbor.Rect.bottom;

            if (expansionAmount > 0)
            {
                newNeighborHeight = (topNeighbor.Rect.bottom - topNeighbor.Rect.top) + expansionAmount;
                int newNeighborBottom = topNeighbor.Rect.top + newNeighborHeight;

                var workArea = focusedWindow.MonitorRect;
                // Validate both edges
                if (topNeighbor.Rect.top < workArea.top || newNeighborBottom > newTop)
                {
                    Logger.Debug("Gap-closing skipped: would exceed screen bounds");
                    topNeighbor = null;
                }
            }
            else if (expansionAmount < 0)
            {
                Logger.Debug($"Gap ({currentGap}px) already smaller than target ({targetGap}px), no adjustment needed");
                topNeighbor = null;
            }
        }

        // Position focused window first
        if (!PositionWindow(focusedWindow, focusedWindow.Rect.left, newTop,
            focusedWindow.Rect.right - focusedWindow.Rect.left, newHeight))
            return false;

        // Then neighbor if validated
        if (topNeighbor != null && expansionAmount > 0)
        {
            Logger.Debug($"Gap-closing: expanding top neighbor by {expansionAmount}px to achieve {AESTHETIC_GAP}px gap");
            PositionWindow(topNeighbor, topNeighbor.Rect.left, topNeighbor.Rect.top,
                topNeighbor.Rect.right - topNeighbor.Rect.left, newNeighborHeight);
        }

        return true;
    }

    private bool ShrinkDown(Window focusedWindow)
    {
        // Shrinking bottom edge means moving it UP (inward)
        // The BOTTOM neighbor should expand UP to fill the gap

        // Calculate new height (shrink from bottom edge)
        int currentHeight = focusedWindow.Rect.bottom - focusedWindow.Rect.top;
        int newHeight = currentHeight - STEP_SIZE;

        // Check minimum size constraint
        if (newHeight < MIN_WINDOW_HEIGHT)
        {
            Logger.Debug($"Cannot shrink below minimum height ({MIN_WINDOW_HEIGHT}px)");
            return false;
        }

        // Use COMPUTED new bottom position
        int newFocusedBottom = focusedWindow.Rect.top + newHeight;

        var bottomNeighbor = WindowNeighborHelper.GetAdjacentWindow(focusedWindow, Direction.Down);
        if (bottomNeighbor == null)
        {
            bottomNeighbor = WindowNeighborHelper.FindWindowForGapClosing(focusedWindow, Direction.Down);
        }

        // Calculate neighbor expansion BEFORE positioning focused window (using COMPUTED newFocusedBottom)
        int expansionAmount = 0;
        int newNeighborTop = 0;
        int newNeighborHeight = 0;
        if (bottomNeighbor != null)
        {
            int currentGap = bottomNeighbor.Rect.top - newFocusedBottom;
            int targetGap = AESTHETIC_GAP;
            int desiredNeighborTop = newFocusedBottom + targetGap;
            expansionAmount = bottomNeighbor.Rect.top - desiredNeighborTop;

            if (expansionAmount > 0)
            {
                newNeighborTop = desiredNeighborTop;
                newNeighborHeight = (bottomNeighbor.Rect.bottom - bottomNeighbor.Rect.top) + expansionAmount;
                int newNeighborBottom = newNeighborTop + newNeighborHeight;

                var workArea = focusedWindow.MonitorRect;
                // Validate BOTH edges
                if (newNeighborTop < workArea.top || newNeighborBottom > workArea.bottom)
                {
                    Logger.Debug("Gap-closing skipped: would exceed screen bounds");
                    bottomNeighbor = null;
                }
            }
            else if (expansionAmount < 0)
            {
                Logger.Debug($"Gap ({currentGap}px) already smaller than target ({targetGap}px), no adjustment needed");
                bottomNeighbor = null;
            }
        }

        // Position focused window first
        if (!PositionWindow(focusedWindow, focusedWindow.Rect.left, focusedWindow.Rect.top,
            focusedWindow.Rect.right - focusedWindow.Rect.left, newHeight))
            return false;

        // Then neighbor if validated
        if (bottomNeighbor != null && expansionAmount > 0)
        {
            Logger.Debug($"Gap-closing: expanding bottom neighbor by {expansionAmount}px to achieve {AESTHETIC_GAP}px gap");
            PositionWindow(bottomNeighbor, bottomNeighbor.Rect.left, newNeighborTop,
                bottomNeighbor.Rect.right - bottomNeighbor.Rect.left, newNeighborHeight);
        }

        return true;
    }

    /// <summary>
    /// Checks if window is at screen edge (within AESTHETIC_GAP tolerance)
    /// </summary>
    private bool IsAtScreenEdge(Window window, Direction direction)
    {
        var workArea = window.MonitorRect;
        return direction switch
        {
            Direction.Left => window.Rect.left <= workArea.left + AESTHETIC_GAP,
            Direction.Right => window.Rect.right >= workArea.right - AESTHETIC_GAP,
            Direction.Up => window.Rect.top <= workArea.top + AESTHETIC_GAP,
            Direction.Down => window.Rect.bottom >= workArea.bottom - AESTHETIC_GAP,
            _ => false
        };
    }

    /// <summary>
    /// Expands window by STEP_SIZE toward screen edge in specified direction
    /// Snaps to edge (with AESTHETIC_GAP) if expansion would go past it
    /// </summary>
    private bool ExpandToScreenEdge(Window window, Direction direction)
    {
        var workArea = window.MonitorRect;

        switch (direction)
        {
            case Direction.Left:
                {
                    // Expand left by STEP_SIZE, or snap to edge if closer
                    int targetLeft = window.Rect.left - STEP_SIZE;
                    int edgeLeft = workArea.left + AESTHETIC_GAP;
                    int newLeft = Math.Max(targetLeft, edgeLeft);
                    int newWidth = window.Rect.right - newLeft;

                    // Validate boundary
                    if (newLeft < workArea.left)
                    {
                        Logger.Debug("Cannot resize beyond left screen edge");
                        return false;
                    }

                    return PositionWindow(window, newLeft, window.Rect.top, newWidth, window.Rect.bottom - window.Rect.top);
                }
            case Direction.Right:
                {
                    // Expand right by STEP_SIZE, or snap to edge if closer
                    int currentWidth = window.Rect.right - window.Rect.left;
                    int targetWidth = currentWidth + STEP_SIZE;
                    int targetRight = window.Rect.left + targetWidth;
                    int edgeRight = workArea.right - AESTHETIC_GAP;
                    int newWidth = (targetRight > edgeRight) ? (edgeRight - window.Rect.left) : targetWidth;

                    // Validate screen boundary
                    int newRight = window.Rect.left + newWidth;
                    if (newRight > workArea.right)
                    {
                        Logger.Debug("Cannot resize beyond right screen edge");
                        return false;
                    }

                    return PositionWindow(window, window.Rect.left, window.Rect.top, newWidth, window.Rect.bottom - window.Rect.top);
                }
            case Direction.Up:
                {
                    // Expand up by STEP_SIZE, or snap to edge if closer
                    int targetTop = window.Rect.top - STEP_SIZE;
                    int edgeTop = workArea.top + AESTHETIC_GAP;
                    int newTop = Math.Max(targetTop, edgeTop);
                    int newHeight = window.Rect.bottom - newTop;

                    // Validate boundary
                    if (newTop < workArea.top)
                    {
                        Logger.Debug("Cannot resize beyond top screen edge");
                        return false;
                    }

                    return PositionWindow(window, window.Rect.left, newTop, window.Rect.right - window.Rect.left, newHeight);
                }
            case Direction.Down:
                {
                    // Expand down by STEP_SIZE, or snap to edge if closer
                    int currentHeight = window.Rect.bottom - window.Rect.top;
                    int targetHeight = currentHeight + STEP_SIZE;
                    int targetBottom = window.Rect.top + targetHeight;
                    int edgeBottom = workArea.bottom - AESTHETIC_GAP;
                    int newHeight = (targetBottom > edgeBottom) ? (edgeBottom - window.Rect.top) : targetHeight;

                    // Validate screen boundary
                    int newBottom = window.Rect.top + newHeight;
                    if (newBottom > workArea.bottom)
                    {
                        Logger.Debug("Cannot resize beyond bottom screen edge");
                        return false;
                    }

                    return PositionWindow(window, window.Rect.left, window.Rect.top, window.Rect.right - window.Rect.left, newHeight);
                }
            default:
                return false;
        }
    }

    /// <summary>
    /// Expands window left edge, optionally shrinking left neighbor
    /// </summary>
    private bool ExpandLeftEdge(Window focusedWindow, Window? leftNeighbor)
    {
        var workArea = focusedWindow.MonitorRect;
        int newLeft = focusedWindow.Rect.left - STEP_SIZE;
        int newWidth = (focusedWindow.Rect.right - focusedWindow.Rect.left) + STEP_SIZE;

        // ALWAYS validate focused window boundary, not just when no neighbor
        if (newLeft <= workArea.left)
        {
            Logger.Debug("Cannot resize beyond left screen edge");
            return false;
        }

        if (leftNeighbor != null)
        {
            // Calculate new width for neighbor (shrink right edge)
            int newNeighborWidth = (leftNeighbor.Rect.right - leftNeighbor.Rect.left) - STEP_SIZE;

            // Check minimum size constraint
            if (newNeighborWidth < MIN_WINDOW_WIDTH)
            {
                Logger.Debug($"Cannot shrink neighbor below minimum width ({MIN_WINDOW_WIDTH}px)");
                // TODO: Future enhancement - automatically reverse to shrink opposite edge
                return false;
            }

            // Ensure left neighbor maintains AESTHETIC_GAP from left screen edge
            // (leftNeighbor.Rect.left stays the same, only width shrinks)
            if (leftNeighbor.Rect.left < workArea.left + AESTHETIC_GAP)
            {
                Logger.Debug($"Cannot shrink neighbor - already at aesthetic gap boundary " +
                             $"(neighborLeft: {leftNeighbor.Rect.left}, boundary: {workArea.left + AESTHETIC_GAP})");
                return false;
            }

            // Verify the shrunk neighbor's right edge doesn't overlap with focused window's new left edge
            int newNeighborRight = leftNeighbor.Rect.left + newNeighborWidth;
            if (newLeft < newNeighborRight + AESTHETIC_GAP)
            {
                Logger.Debug("Cannot shrink neighbor - would overlap with focused window");
                return false;
            }

            // Resize neighbor first (shrink its width, right edge moves left)
            if (!PositionWindow(leftNeighbor, leftNeighbor.Rect.left, leftNeighbor.Rect.top, newNeighborWidth, leftNeighbor.Rect.bottom - leftNeighbor.Rect.top))
                return false;
        }

        // Resize focused window (expand left)
        return PositionWindow(focusedWindow, newLeft, focusedWindow.Rect.top, newWidth, focusedWindow.Rect.bottom - focusedWindow.Rect.top);
    }

    /// <summary>
    /// Expands window right edge, optionally shrinking right neighbor
    /// </summary>
    private bool ExpandRightEdge(Window focusedWindow, Window? rightNeighbor)
    {
        var workArea = focusedWindow.MonitorRect;
        int newWidth = (focusedWindow.Rect.right - focusedWindow.Rect.left) + STEP_SIZE;
        int newRight = focusedWindow.Rect.left + newWidth;

        // ALWAYS validate focused window boundary
        if (newRight >= workArea.right)
        {
            Logger.Debug("Cannot resize beyond right screen edge");
            return false;
        }

        if (rightNeighbor != null)
        {
            // Calculate new position and width for neighbor (move left edge right, shrink)
            int newNeighborLeft = rightNeighbor.Rect.left + STEP_SIZE;
            int newNeighborWidth = (rightNeighbor.Rect.right - rightNeighbor.Rect.left) - STEP_SIZE;

            // Check minimum size constraint
            if (newNeighborWidth < MIN_WINDOW_WIDTH)
            {
                Logger.Debug($"Cannot shrink neighbor below minimum width ({MIN_WINDOW_WIDTH}px)");
                // TODO: Future enhancement - automatically reverse to shrink opposite edge
                return false;
            }

            //Calculate where neighbor's right edge will be
            int newNeighborRight = newNeighborLeft + newNeighborWidth;

            // Ensure neighbor maintains AESTHETIC_GAP from right screen edge
            if (newNeighborRight > workArea.right - AESTHETIC_GAP)
            {
                Logger.Debug($"Cannot shrink neighbor - would push beyond aesthetic gap at screen edge " +
                             $"(newRight: {newNeighborRight}, boundary: {workArea.right - AESTHETIC_GAP})");
                return false;
            }

            // Verify the shrunk neighbor's left edge doesn't overlap with focused window's new right edge
            if (newRight > newNeighborLeft - AESTHETIC_GAP)
            {
                Logger.Debug("Cannot shrink neighbor - would overlap with focused window");
                return false;
            }

            // Resize neighbor first (move left edge right and shrink)
            if (!PositionWindow(rightNeighbor, newNeighborLeft, rightNeighbor.Rect.top, newNeighborWidth, rightNeighbor.Rect.bottom - rightNeighbor.Rect.top))
                return false;
        }

        // Resize focused window (expand right)
        return PositionWindow(focusedWindow, focusedWindow.Rect.left, focusedWindow.Rect.top, newWidth, focusedWindow.Rect.bottom - focusedWindow.Rect.top);
    }

    /// <summary>
    /// Expands window up edge, optionally shrinking top neighbor
    /// </summary>
    private bool ExpandUpEdge(Window focusedWindow, Window? topNeighbor)
    {
        var workArea = focusedWindow.MonitorRect;
        int newTop = focusedWindow.Rect.top - STEP_SIZE;
        int newHeight = (focusedWindow.Rect.bottom - focusedWindow.Rect.top) + STEP_SIZE;

        // ALWAYS validate focused window boundary
        if (newTop <= workArea.top)
        {
            Logger.Debug("Cannot resize beyond top screen edge");
            return false;
        }

        if (topNeighbor != null)
        {
            // Calculate new height for neighbor (shrink bottom edge)
            int newNeighborHeight = (topNeighbor.Rect.bottom - topNeighbor.Rect.top) - STEP_SIZE;

            // Check minimum size constraint
            if (newNeighborHeight < MIN_WINDOW_HEIGHT)
            {
                Logger.Debug($"Cannot shrink neighbor below minimum height ({MIN_WINDOW_HEIGHT}px)");
                // TODO: Future enhancement - automatically reverse to shrink opposite edge
                return false;
            }

            // Ensure top neighbor maintains AESTHETIC_GAP from top screen edge
            // (topNeighbor.Rect.top stays the same, only height shrinks)
            if (topNeighbor.Rect.top < workArea.top + AESTHETIC_GAP)
            {
                Logger.Debug($"Cannot shrink neighbor - already at aesthetic gap boundary " +
                             $"(neighborTop: {topNeighbor.Rect.top}, boundary: {workArea.top + AESTHETIC_GAP})");
                return false;
            }

            // Verify the shrunk neighbor's bottom edge doesn't overlap with focused window's new top edge
            int newNeighborBottom = topNeighbor.Rect.top + newNeighborHeight;
            if (newTop < newNeighborBottom + AESTHETIC_GAP)
            {
                Logger.Debug("Cannot shrink neighbor - would overlap with focused window");
                return false;
            }

            // Resize neighbor first (shrink its height, bottom edge moves up)
            if (!PositionWindow(topNeighbor, topNeighbor.Rect.left, topNeighbor.Rect.top, topNeighbor.Rect.right - topNeighbor.Rect.left, newNeighborHeight))
                return false;
        }

        // Resize focused window (expand up)
        return PositionWindow(focusedWindow, focusedWindow.Rect.left, newTop, focusedWindow.Rect.right - focusedWindow.Rect.left, newHeight);
    }

    /// <summary>
    /// Expands window down edge, optionally shrinking bottom neighbor
    /// </summary>
    private bool ExpandDownEdge(Window focusedWindow, Window? bottomNeighbor)
    {
        var workArea = focusedWindow.MonitorRect;
        int newHeight = (focusedWindow.Rect.bottom - focusedWindow.Rect.top) + STEP_SIZE;
        int newBottom = focusedWindow.Rect.top + newHeight;

        // ALWAYS validate focused window boundary
        if (newBottom >= workArea.bottom)
        {
            Logger.Debug("Cannot resize beyond bottom screen edge");
            return false;
        }

        if (bottomNeighbor != null)
        {
            // Calculate new position and height for neighbor (move top edge down, shrink)
            int newNeighborTop = bottomNeighbor.Rect.top + STEP_SIZE;
            int newNeighborHeight = (bottomNeighbor.Rect.bottom - bottomNeighbor.Rect.top) - STEP_SIZE;

            // Check minimum size constraint
            if (newNeighborHeight < MIN_WINDOW_HEIGHT)
            {
                Logger.Debug($"Cannot shrink neighbor below minimum height ({MIN_WINDOW_HEIGHT}px)");
                // TODO: Future enhancement - automatically reverse to shrink opposite edge
                return false;
            }

            // Calculate where neighbor's bottom edge will be
            int newNeighborBottom = newNeighborTop + newNeighborHeight;

            // Ensure neighbor maintains AESTHETIC_GAP from bottom screen edge
            if (newNeighborBottom > workArea.bottom - AESTHETIC_GAP)
            {
                Logger.Debug($"Cannot shrink neighbor - would push beyond aesthetic gap at screen edge " +
                             $"(newBottom: {newNeighborBottom}, boundary: {workArea.bottom - AESTHETIC_GAP})");
                return false;
            }

            // Verify the shrunk neighbor's top edge doesn't overlap with focused window's new bottom edge
            if (newBottom > newNeighborTop - AESTHETIC_GAP)
            {
                Logger.Debug("Cannot shrink neighbor - would overlap with focused window");
                return false;
            }

            // Resize neighbor first (move top edge down and shrink)
            if (!PositionWindow(bottomNeighbor, bottomNeighbor.Rect.left, newNeighborTop, bottomNeighbor.Rect.right - bottomNeighbor.Rect.left, newNeighborHeight))
                return false;
        }

        // Resize focused window (expand down)
        return PositionWindow(focusedWindow, focusedWindow.Rect.left, focusedWindow.Rect.top, focusedWindow.Rect.right - focusedWindow.Rect.left, newHeight);
    }

    /// <summary>
    /// Positions a window accounting for invisible border offsets
    /// </summary>
    private bool PositionWindow(Window window, int desiredVisibleX, int desiredVisibleY, int desiredVisibleWidth, int desiredVisibleHeight)
    {
        // Calculate border offsets for the window
        // These offsets account for invisible window borders/shadows
        int leftOffset = window.Rect.left - window.ExtendedRect.left;
        int topOffset = window.Rect.top - window.ExtendedRect.top;
        int widthOffset = (window.ExtendedRect.right - window.ExtendedRect.left) - (window.Rect.right - window.Rect.left);
        int heightOffset = (window.ExtendedRect.bottom - window.ExtendedRect.top) - (window.Rect.bottom - window.Rect.top);

        // Adjust for border offsets to ensure visible areas align correctly
        int targetX = desiredVisibleX - leftOffset;
        int targetY = desiredVisibleY - topOffset;
        int targetWidth = desiredVisibleWidth + widthOffset;
        int targetHeight = desiredVisibleHeight + heightOffset;

        // Validate the VISIBLE window position stays within work area
        // Allow ExtendedRect (shadows) to extend 4-8px beyond workArea - this is normal Windows behavior
        var workArea = window.MonitorRect;
        int visibleRight = desiredVisibleX + desiredVisibleWidth;
        int visibleBottom = desiredVisibleY + desiredVisibleHeight;

        if (desiredVisibleX < workArea.left || desiredVisibleY < workArea.top ||
            visibleRight > workArea.right || visibleBottom > workArea.bottom)
        {
            Logger.Warning($"Attempted to position window's visible area outside work area bounds. " +
                           $"Visible: ({desiredVisibleX}, {desiredVisibleY}, {visibleRight}, {visibleBottom}), " +
                           $"WorkArea: ({workArea.left}, {workArea.top}, {workArea.right}, {workArea.bottom})");
            return false;
        }

        Logger.Debug($"Positioning window '{window.Text}' to: x={targetX}, y={targetY}, w={targetWidth}, h={targetHeight}");

        bool result = PInvoke.SetWindowPos(
            window.Handle,
            HWND.Null,
            targetX,
            targetY,
            targetWidth,
            targetHeight,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
        );

        return result;
    }
}
