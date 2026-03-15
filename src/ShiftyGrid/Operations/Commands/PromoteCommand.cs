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
            "Toggle promotion of foreground window to specified coordinates");

        var coordinatesOption = new Option<string>(
            aliases: ["--coordinates", "-c"],
            description: "Coordinates: startX,startY,endX,endY (e.g., \"1,0,11,12\")"
        ) { IsRequired = true };

        var gridOption = new Option<string>(
            aliases: ["--grid", "-g"],
            description: "Grid size in format NxM (e.g., \"12x12\")",
            getDefaultValue: () => "12x12"
        );

        promoteCommand.AddOption(coordinatesOption);
        promoteCommand.AddOption(gridOption);

        promoteCommand.SetHandler(
            async (coordinatesStr, gridStr) =>
            {
                try
                {
                    var coordinates = Coordinates.Parse(coordinatesStr, gridStr);
                    await SendAsync(coordinates);
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            },
            coordinatesOption,
            gridOption
        );

        return promoteCommand;
    }

    public async Task SendAsync(Coordinates coordinates) =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name, coordinates);
}
