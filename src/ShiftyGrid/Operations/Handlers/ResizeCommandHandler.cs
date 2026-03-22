using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Common;

namespace ShiftyGrid.Operations.Handlers;


internal enum WindowResize
{
    ExpandLeft,
    ExpandRight,
    ShrinkLeft,
    ShrinkRight,
    ExpandUp,
    ExpandDown,
    ShrinkUp,
    ShrinkDown,
    OuterLeft,
    OuterRight,
    OuterUp,
    OuterDown,
}

internal enum ResizeOperation
{
    Expand,
    Shrink
}

/// <summary>
/// Encapsulates the results of edge movement calculation, including new positions for both
/// the focused window and its affected neighbor.
/// </summary>
internal readonly record struct EdgeMovement(
    Coordinates FocusedNewPos,
    Coordinates? NeighborNewPos,
    Window? AffectedNeighbor,
    string MovingEdgeName);

/// <summary>
/// Holds horizontal neighbor information for position-aware edge selection.
/// </summary>
internal readonly record struct HorizontalNeighbors(Window? Left, Window? Right);

/// <summary>
/// Holds vertical neighbor information for position-aware edge selection.
/// </summary>
internal readonly record struct VerticalNeighbors(Window? Top, Window? Bottom);

/// <summary>
/// Handles window resize based on defined grid.
/// 
/// Neighbor Adjustments:
/// - When a window's edge moves, adjacent neighbor windows adjust automatically
/// - The total space between windows remains constant (zero-sum resizing)
/// - Prevents gaps or overlaps in the layout
/// - Windows cannot get smaller than minimal grid size
/// 
/// </summary>
internal class ResizeCommandHandler : RequestHandler<WindowResize>
{
    private const int MIN_WINDOW_GRID_SIZE = 2;   // Minimum window size in grid units (prevents windows from becoming too small)

    private readonly Grid _grid;
    private readonly WindowNavigationService _WindowNavigationService;
    private readonly int _gap;

    public ResizeCommandHandler(WindowNavigationService WindowNavigationService, int gap, Grid grid)
    {
        _WindowNavigationService = WindowNavigationService ?? throw new ArgumentNullException(nameof(WindowNavigationService));
        _gap = gap;
        _grid = grid;
    }

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

    private bool Execute(WindowResize resizeStep)
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
            WindowResize.ExpandLeft => ResizeHorizontal(activeWindow, Direction.Left, ResizeOperation.Expand),
            WindowResize.ExpandRight => ResizeHorizontal(activeWindow, Direction.Right, ResizeOperation.Expand),
            WindowResize.ShrinkLeft => ResizeHorizontal(activeWindow, Direction.Left, ResizeOperation.Shrink),
            WindowResize.ShrinkRight => ResizeHorizontal(activeWindow, Direction.Right, ResizeOperation.Shrink),
            WindowResize.ExpandUp => ResizeVertical(activeWindow, Direction.Up, ResizeOperation.Expand),
            WindowResize.ExpandDown => ResizeVertical(activeWindow, Direction.Down, ResizeOperation.Expand),
            WindowResize.ShrinkUp => ResizeVertical(activeWindow, Direction.Up, ResizeOperation.Shrink),
            WindowResize.ShrinkDown => ResizeVertical(activeWindow, Direction.Down, ResizeOperation.Shrink),
            WindowResize.OuterLeft => ResizeHorizontal(activeWindow, Direction.Left, ResizeOperation.Expand, outer: true),
            WindowResize.OuterRight => ResizeHorizontal(activeWindow, Direction.Right, ResizeOperation.Expand, outer: true),
            WindowResize.OuterUp => ResizeVertical(activeWindow, Direction.Up, ResizeOperation.Expand, outer: true),
            WindowResize.OuterDown => ResizeVertical(activeWindow, Direction.Down, ResizeOperation.Expand, outer: true),
            _ => false
        };
    }

    private bool ResizeHorizontal(Window focused, Direction direction, ResizeOperation operation, bool outer = false)
    {
        // 1. Convert to grid coordinates
        var currentPos = WindowPositioner.ConvertWindowToGridPosition(focused, _grid);

        // 2. Log start
        string operationName = GetOperationName(direction, operation, isHorizontal: true);
        Logger.Debug($"[{operationName}] START: {focused.Text}");
        Logger.Debug($"  Current grid position: [{currentPos.StartX},{currentPos.StartY}] to [{currentPos.EndX},{currentPos.EndY}]");
        Logger.Debug($"  Grid width: {currentPos.EndX - currentPos.StartX} units");

        // 3. Get neighbors
        var neighbors = GetHorizontalNeighbors(focused);

        // 4. Calculate edge movement
        var movement = CalculateHorizontalMovement(currentPos, direction, operation, neighbors, outer);

        Logger.Debug($"  Moving {movement.MovingEdgeName} edge");

        // 5. Validate focused window
        if (!ValidateGridPosition(movement.FocusedNewPos, "Focused window"))
            return false;

        // 6. Handle neighbor adjustment
        if (movement.AffectedNeighbor != null && movement.NeighborNewPos.HasValue)
        {
            if (!PositionNeighbor(movement.AffectedNeighbor, movement.NeighborNewPos.Value, movement.MovingEdgeName))
                return false;
        }

        // 7. Position focused window
        Logger.Debug($"  New focused grid: [{movement.FocusedNewPos.StartX},{movement.FocusedNewPos.StartY}] to [{movement.FocusedNewPos.EndX},{movement.FocusedNewPos.EndY}]");

        if (!WindowPositioner.ChangePosition(focused, movement.FocusedNewPos, _gap))
        {
            Logger.Error("Failed to position focused window");
            return false;
        }

        Logger.Debug($"[{operationName}] SUCCESS");
        return true;
    }

    private EdgeMovement CalculateHorizontalMovement(Coordinates currentPos, Direction direction,
        ResizeOperation operation, HorizontalNeighbors neighbors, bool outer = false)
    {
        bool isLeftmost = neighbors.Left == null && neighbors.Right != null;
        bool movingRightEdge;
        int delta;

        // Determine which edge to move and in which direction
        if (operation == ResizeOperation.Expand)
        {
            if (direction == Direction.Right)
            {
                movingRightEdge = isLeftmost;
                delta = 1;
            }
            else // Direction.Left
            {
                movingRightEdge = isLeftmost;
                delta = -1;
            }
        }
        else // ResizeOperation.Shrink
        {
            movingRightEdge = !isLeftmost;
            delta = direction == Direction.Right ? 1 : -1;
        }

        // Outer modifier flips to the opposite border
        if (outer) movingRightEdge = !movingRightEdge;

        // Calculate new position for focused window
        Coordinates focusedNewPos = movingRightEdge
            ? new Coordinates(_grid, currentPos.StartX, currentPos.StartY, currentPos.EndX + delta, currentPos.EndY)
            : new Coordinates(_grid, currentPos.StartX + delta, currentPos.StartY, currentPos.EndX, currentPos.EndY);

        // Determine affected neighbor and calculate its new position
        Window? affectedNeighbor = movingRightEdge ? neighbors.Right : neighbors.Left;
        Coordinates? neighborNewPos = null;

        if (affectedNeighbor != null)
        {
            var neighborPos = WindowPositioner.ConvertWindowToGridPosition(affectedNeighbor, _grid);

            neighborNewPos = movingRightEdge
                ? new Coordinates(_grid, neighborPos.StartX + delta, neighborPos.StartY, neighborPos.EndX, neighborPos.EndY)
                : new Coordinates(_grid, neighborPos.StartX, neighborPos.StartY, neighborPos.EndX + delta, neighborPos.EndY);
        }

        string edgeName = movingRightEdge ? "right" : "left";
        return new EdgeMovement(focusedNewPos, neighborNewPos, affectedNeighbor, edgeName);
    }

    private bool ResizeVertical(Window focused, Direction direction, ResizeOperation operation, bool outer = false)
    {
        // 1. Convert to grid coordinates
        var currentPos = WindowPositioner.ConvertWindowToGridPosition(focused, _grid);

        // 2. Log start
        string operationName = GetOperationName(direction, operation, isHorizontal: false);
        Logger.Debug($"[{operationName}] START: {focused.Text}");
        Logger.Debug($"  Current grid position: [{currentPos.StartX},{currentPos.StartY}] to [{currentPos.EndX},{currentPos.EndY}]");
        Logger.Debug($"  Grid height: {currentPos.EndY - currentPos.StartY} units");

        // 3. Get neighbors
        var neighbors = GetVerticalNeighbors(focused);

        // 4. Calculate edge movement
        var movement = CalculateVerticalMovement(currentPos, direction, operation, neighbors, outer);

        Logger.Debug($"  Moving {movement.MovingEdgeName} edge");

        // 5. Validate focused window
        if (!ValidateGridPosition(movement.FocusedNewPos, "Focused window"))
            return false;

        // 6. Handle neighbor adjustment
        if (movement.AffectedNeighbor != null && movement.NeighborNewPos.HasValue)
        {
            if (!PositionNeighbor(movement.AffectedNeighbor, movement.NeighborNewPos.Value, movement.MovingEdgeName))
                return false;
        }

        // 7. Position focused window
        Logger.Debug($"  New focused grid: [{movement.FocusedNewPos.StartX},{movement.FocusedNewPos.StartY}] to [{movement.FocusedNewPos.EndX},{movement.FocusedNewPos.EndY}]");

        if (!WindowPositioner.ChangePosition(focused, movement.FocusedNewPos, _gap))
        {
            Logger.Error("Failed to position focused window");
            return false;
        }

        Logger.Debug($"[{operationName}] SUCCESS");
        return true;
    }

    /// <summary>
    /// Calculates the edge movement for vertical resize operations based on window position and operation type.
    /// </summary>
    private EdgeMovement CalculateVerticalMovement(Coordinates currentPos, Direction direction,
        ResizeOperation operation, VerticalNeighbors neighbors, bool outer = false)
    {
        bool isTopmost = neighbors.Top == null && neighbors.Bottom != null;
        bool movingBottomEdge;
        int delta;

        // Determine which edge to move and in which direction
        if (operation == ResizeOperation.Expand)
        {
            if (direction == Direction.Down)
            {
                movingBottomEdge = isTopmost;
                delta = 1;
            }
            else // Direction.Up
            {
                movingBottomEdge = isTopmost;
                delta = -1;
            }
        }
        else // ResizeOperation.Shrink
        {
            movingBottomEdge = !isTopmost;
            delta = direction == Direction.Down ? 1 : -1;
        }

        // Outer modifier flips to the opposite border
        if (outer) movingBottomEdge = !movingBottomEdge;

        // Calculate new position for focused window
        Coordinates focusedNewPos = movingBottomEdge
            ? new Coordinates(_grid, currentPos.StartX, currentPos.StartY, currentPos.EndX, currentPos.EndY + delta)
            : new Coordinates(_grid, currentPos.StartX, currentPos.StartY + delta, currentPos.EndX, currentPos.EndY);

        // Determine affected neighbor and calculate its new position
        Window? affectedNeighbor = movingBottomEdge ? neighbors.Bottom : neighbors.Top;
        Coordinates? neighborNewPos = null;

        if (affectedNeighbor != null)
        {
            var neighborPos = WindowPositioner.ConvertWindowToGridPosition(affectedNeighbor, _grid);

            neighborNewPos = movingBottomEdge
                ? new Coordinates(_grid, neighborPos.StartX, neighborPos.StartY + delta, neighborPos.EndX, neighborPos.EndY)
                : new Coordinates(_grid, neighborPos.StartX, neighborPos.StartY, neighborPos.EndX, neighborPos.EndY + delta);
        }

        string edgeName = movingBottomEdge ? "bottom" : "top";
        return new EdgeMovement(focusedNewPos, neighborNewPos, affectedNeighbor, edgeName);
    }

    private HorizontalNeighbors GetHorizontalNeighbors(Window focused)
    {
        var left = _WindowNavigationService.GetAdjacentWindow(focused, Direction.Left);
        var right = _WindowNavigationService.GetAdjacentWindow(focused, Direction.Right);
        return new HorizontalNeighbors(left, right);
    }

    private VerticalNeighbors GetVerticalNeighbors(Window focused)
    {
        var top = _WindowNavigationService.GetAdjacentWindow(focused, Direction.Up);
        var bottom = _WindowNavigationService.GetAdjacentWindow(focused, Direction.Down);
        return new VerticalNeighbors(top, bottom);
    }

    private bool PositionNeighbor(Window neighbor, Coordinates newPos, string movingEdgeName)
    {
        Logger.Debug($"  {movingEdgeName} neighbor found: {neighbor.Text}");

        var neighborPos = WindowPositioner.ConvertWindowToGridPosition(neighbor, _grid);
        Logger.Debug($"    Current grid: [{neighborPos.StartX},{neighborPos.StartY}] to [{neighborPos.EndX},{neighborPos.EndY}]");
        Logger.Debug($"    New grid: [{newPos.StartX},{newPos.StartY}] to [{newPos.EndX},{newPos.EndY}]");

        // Validate neighbor
        if (!ValidateGridPosition(newPos, "Neighbor window"))
            return false;

        // Position neighbor first
        if (!WindowPositioner.ChangePosition(neighbor, newPos, _gap))
        {
            Logger.Error("Failed to position neighbor");
            return false;
        }

        return true;
    }

    private string GetOperationName(Direction direction, ResizeOperation operation, bool isHorizontal)
    {
        if (operation == ResizeOperation.Expand)
        {
            return direction switch
            {
                Direction.Left => "ResizeLeft",
                Direction.Right => "ResizeRight",
                Direction.Up => "ResizeUp",
                Direction.Down => "ResizeDown",
                _ => "UnknownResize"
            };
        }
        else // Shrink
        {
            return direction switch
            {
                Direction.Left => "ShrinkLeft",
                Direction.Right => "ShrinkRight",
                Direction.Up => "ShrinkUp",
                Direction.Down => "ShrinkDown",
                _ => "UnknownShrink"
            };
        }
    }

    /// <summary>
    /// Validates that a grid position meets minimum size and boundary constraints
    /// </summary>
    private bool ValidateGridPosition(Coordinates position, string contextName)
    {
        // Width validation
        int gridWidth = position.EndX - position.StartX;
        if (gridWidth < MIN_WINDOW_GRID_SIZE)
        {
            Logger.Debug($"  BLOCKED: {contextName} would be too narrow ({gridWidth} grid units < {MIN_WINDOW_GRID_SIZE} minimum)");
            return false;
        }

        // Height validation
        int gridHeight = position.EndY - position.StartY;
        if (gridHeight < MIN_WINDOW_GRID_SIZE)
        {
            Logger.Debug($"  BLOCKED: {contextName} would be too short ({gridHeight} grid units < {MIN_WINDOW_GRID_SIZE} minimum)");
            return false;
        }

        // Boundary validation
        if (position.StartX < 0 || position.EndX > position.Grid.Columns ||
            position.StartY < 0 || position.EndY > position.Grid.Rows)
        {
            Logger.Debug($"  BLOCKED: {contextName} would exceed grid boundaries");
            return false;
        }

        return true;
    }
}
