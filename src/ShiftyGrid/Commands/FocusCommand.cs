using ShiftyGrid.Handlers;

namespace ShiftyGrid.Commands;

internal class FocusCommand : BaseCommand
{
    public const string Name = "focus";

    public async Task SendAsync(Direction direction) =>
        await SendRequestAsync("Sending {Name} command to running instance...", Name, direction);
}
