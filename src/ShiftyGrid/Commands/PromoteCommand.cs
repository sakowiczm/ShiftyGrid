namespace ShiftyGrid.Commands;

/// <summary>
/// Promote active window to main position CenterWide. Shortcut window gets back to it's pervious position.
/// If we have promoted window and we want to promote new window, old window automatically goes back to its
/// original location.
/// </summary>
public class PromoteCommand : BaseCommand
{
    public const string Name = "promote";

    public async Task SendAsync(string data) =>
        await SendRequestAsync("Sending promote command to running instance...", Name, data);
}
