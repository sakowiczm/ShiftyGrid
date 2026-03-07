namespace ShiftyGrid.Commands;

/// <summary>
/// Arranges up to 3 windows in equal vertical columns (33% width each).
/// Uses smart positioning when fewer than 3 windows are available.
/// </summary>
internal class ArrangeColumnsCommand : BaseCommand
{
    public const string Name = "arrange-columns";

    public async Task SendAsync(string data) =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name, data);
}
