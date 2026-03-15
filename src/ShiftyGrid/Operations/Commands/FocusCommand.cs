using ShiftyGrid.Infrastructure.Models;
using System.CommandLine;

namespace ShiftyGrid.Operations.Commands;

internal class FocusCommand : BaseCommand
{
    public const string Name = "focus";

    public Command Create()
    {
        var focusCommand = new Command(Name, "Move focus to the adjacent window in the specified direction");

        var directionArgument = new Argument<Direction>(
            name: "direction",
            description: "Direction: Left, Right, Up, Down"
        );

        focusCommand.AddArgument(directionArgument);
        focusCommand.SetHandler(
            async (direction) => await SendAsync(direction),
            directionArgument
        );

        return focusCommand;
    }

    public async Task SendAsync(Direction direction) =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name, direction);
}