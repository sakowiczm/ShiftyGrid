namespace ShiftyGrid.Commands;

/// <summary>
/// Arranges up to 4 windows in screen quadrants (25% area each).
/// Uses smart positioning when fewer than 4 windows are available.
/// </summary>
internal class ArrangeCornersCommand : BaseCommand
{
    public const string Name = "arrange-corners";

    public async Task SendAsync(string data) =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name, data);
}
