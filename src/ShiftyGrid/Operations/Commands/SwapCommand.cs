using ShiftyGrid.Infrastructure.Models;
using System.CommandLine;

namespace ShiftyGrid.Operations.Commands;

internal class SwapCommand : BaseCommand
{
    public const string Name = "swap";

    public Command Create()
    {
        var swapCommand = new Command(Name, "Swap positions with adjacent window");

        var directionArgument = new Argument<Direction>(
            name: "direction",
            description: "Direction: Left, Right, Up, Down"
        );

        swapCommand.AddArgument(directionArgument);
        swapCommand.SetHandler(
            async (direction) => await SendAsync(direction),
            directionArgument
        );

        return swapCommand;
    }

    public async Task SendAsync(Direction direction) =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name, direction);
}
