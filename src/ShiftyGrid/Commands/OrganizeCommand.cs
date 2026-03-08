namespace ShiftyGrid.Commands;

/// <summary>
/// Organize visible windows on the current monitor according to predefined position rules.
/// </summary>
public class OrganizeCommand : BaseCommand
{
    public const string Name = "organize";

    public async Task SendAsync() =>
        await SendRequestAsync("Organizing windows on current monitor...", Name);
}
