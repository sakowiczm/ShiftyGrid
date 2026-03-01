namespace ShiftyGrid.Commands;

// todo: add different arrange options - 2,3,4 columns, arranged windows in quaters, arrange window up and below on one half of the screen.
//  how to pick windows? active one + any adjecent + next windows in the z-order


// todo: if we do the split of two windows - do the windows need to be adjecent? e.g if there is no adjecent windows take focused window and 
//  other window not focused but but fully visible window to the user (not obscured) and expand it?


/// <summary>
/// Idea windows were resized and now I want equal split again.
/// </summary>
internal class ArrangeSplitCommand : BaseCommand
{
    public const string Name = "arrange-split";

    public async Task SendAsync(string data) =>
        await SendRequestAsync("Sending {Name} command to running instance...", Name, data);
}
