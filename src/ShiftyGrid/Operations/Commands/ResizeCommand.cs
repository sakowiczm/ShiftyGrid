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

        var directionOption = new Option<Direction>(
            name: "--direction",
            description: "Direction: Left, Right, Up, Down"
        )
        { IsRequired = true };

        var outerOption = new Option<bool>(
            name: "--outer",
            description: "Move the outer border instead of the inner border"
        );

        resizeCommand.AddOption(directionOption);
        resizeCommand.AddOption(outerOption);
        resizeCommand.SetHandler(
            async (direction, outer) => await Execute(direction, outer),
            directionOption, outerOption
        );

        return resizeCommand;
    }

    public async Task Execute(Direction direction, bool outer = false)
    {
        WindowResize windowResize;

        if (outer)
        {
            windowResize = direction switch
            {
                Direction.Left => WindowResize.OuterLeft,
                Direction.Right => WindowResize.OuterRight,
                Direction.Up => WindowResize.OuterUp,
                Direction.Down => WindowResize.OuterDown,
                _ => throw new ArgumentException($"Invalid direction: {direction}")
            };
        }
        else
        {
            windowResize = direction switch
            {
                Direction.Left => WindowResize.ExpandLeft,
                Direction.Right => WindowResize.ExpandRight,
                Direction.Up => WindowResize.ExpandUp,
                Direction.Down => WindowResize.ExpandDown,
                _ => throw new ArgumentException($"Invalid direction: {direction}")
            };
        }

        await SendRequestAsync($"Sending {Name} command to running instance...", Name, windowResize);
    }
}