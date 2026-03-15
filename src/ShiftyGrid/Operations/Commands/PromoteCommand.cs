using ShiftyGrid.Infrastructure.Models;
using System.CommandLine;

namespace ShiftyGrid.Operations.Commands;

/// <summary>
/// Promote active window to specified position. Promoted window gets back to its previous position on second call.
/// If we have promoted window and we want to promote new window, old window automatically goes back to its
/// original location.
/// </summary>
public class PromoteCommand : BaseCommand
{
    public const string Name = "promote";

    public Command Create()
    {
        var promoteCommand = new Command(Name,
            "Toggle promotion of foreground window to specified position");

        var positionOption = new Option<string>(
            aliases: ["--position", "-p"],
            description: "Position coordinates: startX,startY,endX,endY (e.g., \"1,0,11,12\")"
        ) { IsRequired = true };

        var gridOption = new Option<string>(
            aliases: ["--grid", "-g"],
            description: "Grid size in format NxM (e.g., \"12x12\")",
            getDefaultValue: () => "12x12"
        );

        promoteCommand.AddOption(positionOption);
        promoteCommand.AddOption(gridOption);

        promoteCommand.SetHandler(
            async (positionStr, gridStr) =>
            {
                try
                {
                    var position = Position.Parse(positionStr, gridStr);
                    await SendAsync(position);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            },
            positionOption,
            gridOption
        );

        return promoteCommand;
    }

    public async Task SendAsync(Position position) =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name, position);
}
