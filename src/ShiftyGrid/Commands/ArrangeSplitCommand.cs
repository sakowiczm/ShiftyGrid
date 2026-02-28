namespace ShiftyGrid.Commands;

// todo: add different arrange options - 2,3,4 columns, arranged windows in quaters, arrange window up and below on one half of the screen.
//  how to pick windows? active one + any adjecent + next windows in the z-order


/// <summary>
/// Idea windows were resized and now I want equal split again.
/// </summary>
internal class ArrangeSplitCommand : BaseCommand
{
    public const string Name = "arrange-split";

    public async Task SendAsync(string data) =>
        await SendRequestAsync("Sending {Name} command to running instance...", Name, data);
}
