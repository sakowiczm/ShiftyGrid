using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.Text.Json.Serialization.Metadata;

namespace ShiftyGrid.Handlers;

// todo: issue when initally window is samller than minimal size
// todo: resize issues up or down - single window
// todo: pass minimal size to ConvertWindowToGridPosition
// todo: remove WindowResize as it's no longer necessary

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
    Position FocusedNewPos,
    Position? NeighborNewPos,
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
    private static readonly Grid DEFAULT_GRID = new Grid(12, 12);

    private readonly WindowNeighborHelper _windowNeighborHelper;
    private readonly int _gap;

    public ResizeCommandHandler(WindowNeighborHelper windowNeighborHelper, int gap)
    {
        _windowNeighborHelper = windowNeighborHelper ?? throw new ArgumentNullException(nameof(windowNeighborHelper));
        _gap = gap;
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
            _ => false
        };
    }

    private bool ResizeHorizontal(Window focused, Direction direction, ResizeOperation operation)
    {
        // 1. Convert to grid coordinates
        var currentPos = GridCoordinateConverter.ConvertWindowToGridPosition(focused, DEFAULT_GRID);

        // 2. Log start
        string operationName = GetOperationName(direction, operation, isHorizontal: true);
        Logger.Debug($"[{operationName}] START: {focused.Text}");
        Logger.Debug($"  Current grid position: [{currentPos.StartX},{currentPos.StartY}] to [{currentPos.EndX},{currentPos.EndY}]");
        Logger.Debug($"  Grid width: {currentPos.EndX - currentPos.StartX} units");

        // 3. Get neighbors
        var neighbors = GetHorizontalNeighbors(focused);

        // 4. Calculate edge movement
        var movement = CalculateHorizontalMovement(currentPos, direction, operation, neighbors);

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

    private EdgeMovement CalculateHorizontalMovement(Position currentPos, Direction direction,
        ResizeOperation operation, HorizontalNeighbors neighbors)
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

        // Calculate new position for focused window
        Position focusedNewPos = movingRightEdge
            ? new Position(DEFAULT_GRID, currentPos.StartX, currentPos.StartY, currentPos.EndX + delta, currentPos.EndY)
            : new Position(DEFAULT_GRID, currentPos.StartX + delta, currentPos.StartY, currentPos.EndX, currentPos.EndY);

        // Determine affected neighbor and calculate its new position
        Window? affectedNeighbor = movingRightEdge ? neighbors.Right : neighbors.Left;
        Position? neighborNewPos = null;

        if (affectedNeighbor != null)
        {
            var neighborPos = GridCoordinateConverter.ConvertWindowToGridPosition(affectedNeighbor, DEFAULT_GRID);

            neighborNewPos = movingRightEdge
                ? new Position(DEFAULT_GRID, neighborPos.StartX + delta, neighborPos.StartY, neighborPos.EndX, neighborPos.EndY)
                : new Position(DEFAULT_GRID, neighborPos.StartX, neighborPos.StartY, neighborPos.EndX + delta, neighborPos.EndY);
        }

        string edgeName = movingRightEdge ? "right" : "left";
        return new EdgeMovement(focusedNewPos, neighborNewPos, affectedNeighbor, edgeName);
    }

    private bool ResizeVertical(Window focused, Direction direction, ResizeOperation operation)
    {
        // 1. Convert to grid coordinates
        var currentPos = GridCoordinateConverter.ConvertWindowToGridPosition(focused, DEFAULT_GRID);

        // 2. Log start
        string operationName = GetOperationName(direction, operation, isHorizontal: false);
        Logger.Debug($"[{operationName}] START: {focused.Text}");
        Logger.Debug($"  Current grid position: [{currentPos.StartX},{currentPos.StartY}] to [{currentPos.EndX},{currentPos.EndY}]");
        Logger.Debug($"  Grid height: {currentPos.EndY - currentPos.StartY} units");

        // 3. Get neighbors
        var neighbors = GetVerticalNeighbors(focused);

        // 4. Calculate edge movement
        var movement = CalculateVerticalMovement(currentPos, direction, operation, neighbors);

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
    private EdgeMovement CalculateVerticalMovement(Position currentPos, Direction direction,
        ResizeOperation operation, VerticalNeighbors neighbors)
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

        // Calculate new position for focused window
        Position focusedNewPos = movingBottomEdge
            ? new Position(DEFAULT_GRID, currentPos.StartX, currentPos.StartY, currentPos.EndX, currentPos.EndY + delta)
            : new Position(DEFAULT_GRID, currentPos.StartX, currentPos.StartY + delta, currentPos.EndX, currentPos.EndY);

        // Determine affected neighbor and calculate its new position
        Window? affectedNeighbor = movingBottomEdge ? neighbors.Bottom : neighbors.Top;
        Position? neighborNewPos = null;

        if (affectedNeighbor != null)
        {
            var neighborPos = GridCoordinateConverter.ConvertWindowToGridPosition(affectedNeighbor, DEFAULT_GRID);

            neighborNewPos = movingBottomEdge
                ? new Position(DEFAULT_GRID, neighborPos.StartX, neighborPos.StartY + delta, neighborPos.EndX, neighborPos.EndY)
                : new Position(DEFAULT_GRID, neighborPos.StartX, neighborPos.StartY, neighborPos.EndX, neighborPos.EndY + delta);
        }

        string edgeName = movingBottomEdge ? "bottom" : "top";
        return new EdgeMovement(focusedNewPos, neighborNewPos, affectedNeighbor, edgeName);
    }

    private HorizontalNeighbors GetHorizontalNeighbors(Window focused)
    {
        var left = _windowNeighborHelper.GetAdjacentWindow(focused, Direction.Left);
        var right = _windowNeighborHelper.GetAdjacentWindow(focused, Direction.Right);
        return new HorizontalNeighbors(left, right);
    }

    private VerticalNeighbors GetVerticalNeighbors(Window focused)
    {
        var top = _windowNeighborHelper.GetAdjacentWindow(focused, Direction.Up);
        var bottom = _windowNeighborHelper.GetAdjacentWindow(focused, Direction.Down);
        return new VerticalNeighbors(top, bottom);
    }

    private bool PositionNeighbor(Window neighbor, Position newPos, string movingEdgeName)
    {
        Logger.Debug($"  {movingEdgeName} neighbor found: {neighbor.Text}");

        var neighborPos = GridCoordinateConverter.ConvertWindowToGridPosition(neighbor, DEFAULT_GRID);
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
    private bool ValidateGridPosition(Position position, string contextName)
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
