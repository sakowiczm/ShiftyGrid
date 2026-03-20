using System.CommandLine;
using ShiftyGrid.Operations.Handlers;

namespace ShiftyGrid.Operations.Commands;

/// <summary>
/// Organize visible windows on the current monitor according to predefined coordinates.
/// </summary>
public class OrganizeCommand : BaseCommand
{
    public const string Name = "organize";

    public Command Create()
    {
        var organizeCommand = new Command(Name,
            "Organize visible windows on current monitor according to predefined rules");

        var allOption = new Option<bool>(
            aliases: ["--all", "-a"],
            description: "Organize windows across all monitors instead of just the current monitor",
            getDefaultValue: () => false
        );

        var windowOption = new Option<string?>(
            aliases: ["--window", "-w"],
            description: "Window handle (HWND) to organize instead of the foreground window"
        );

        organizeCommand.AddOption(allOption);
        organizeCommand.AddOption(windowOption);

        organizeCommand.SetHandler(
            async (allMonitors, hwnd) => await SendAsync(allMonitors, hwnd),
            allOption, windowOption
        );

        return organizeCommand;
    }

    public async Task SendAsync(bool processAllMonitors, string? hwnd = null)
    {
        var options = new OrganizeOptions(processAllMonitors, hwnd);
        await SendRequestAsync(
            $"Sending {Name} command to running instance...",
            Name,
            options
        );
    }
}
