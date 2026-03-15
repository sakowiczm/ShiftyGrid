using System.CommandLine;

namespace ShiftyGrid.Operations.Commands;

/// <summary>
/// Organize visible windows on the current monitor according to predefined position rules.
/// </summary>
public class OrganizeCommand : BaseCommand
{
    public const string Name = "organize";

    public Command Create()
    {
        var organizeCommand = new Command(Name,
            "Organize visible windows on current monitor according to predefined rules");
        organizeCommand.SetHandler(async () => await SendAsync());
        return organizeCommand;
    }

    public async Task SendAsync() =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name);
}
