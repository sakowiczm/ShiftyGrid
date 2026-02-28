namespace ShiftyGrid.Commands;

/// <summary>
/// Idea windows were resized and now I want equal split again.
/// </summary>
internal class ArrangeSplitCommand : BaseCommand
{
    public const string Name = "arrange-split";

    public async Task SendAsync(string data) =>
        await SendRequestAsync("Sending {Name} command to running instance...", Name, data);
}
