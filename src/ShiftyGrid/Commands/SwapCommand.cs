using ShiftyGrid.Handlers;

namespace ShiftyGrid.Commands;

internal class SwapCommand : BaseCommand
{
    public const string Name = "swap";

    public async Task SendAsync(Direction direction) =>
        await SendRequestAsync("Sending {Name} command to running instance...", Name, direction);
}
