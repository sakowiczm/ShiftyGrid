using System.CommandLine;
using ShiftyGrid.Infrastructure.Models;
using ShiftyGrid.Operations.Handlers;

namespace ShiftyGrid.Operations.Commands;

internal class ResizeCommand : BaseCommand
{
    public const string Name = "resize";

    public Command Create()
    {
        var resizeCommand = new Command(Name, "Resize the foreground window");

        var directionArgument = new Argument<Direction>(
            name: "direction",
            description: "Direction: Left, Right, Up, Down"
        );

        resizeCommand.AddArgument(directionArgument);
        resizeCommand.SetHandler(
            async (direction) => await Execute(direction),
            directionArgument
        );

        return resizeCommand;
    }

    public async Task Execute(Direction direction)
    {
        // Map Direction to WindowResize (defaulting to Expand operation)
        var windowResize = direction switch
        {
            Direction.Left => WindowResize.ExpandLeft,
            Direction.Right => WindowResize.ExpandRight,
            Direction.Up => WindowResize.ExpandUp,
            Direction.Down => WindowResize.ExpandDown,
            _ => throw new ArgumentException($"Invalid direction: {direction}")
        };

        await SendRequestAsync($"Sending {Name} command to running instance...", Name, windowResize);
    }
}
