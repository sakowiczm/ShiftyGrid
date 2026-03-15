using ShiftyGrid.Infrastructure.Models;
using System.CommandLine;

namespace ShiftyGrid.Operations.Commands;

public class MoveCommand : BaseCommand
{
    public const string Name = "move";

    public Command Create()
    {
        var moveCommand = new Command(Name, "Move the foreground window to specified grid coordinates");

        var coordinatesOption = new Option<string>(
            aliases: ["--coordinates", "-c"],
            description: "Coordinates: startX,startY,endX,endY (e.g., \"0,0,6,12\")"
        )
        { IsRequired = true };

        var gridOption = new Option<string>(
            aliases: ["--grid", "-g"],
            getDefaultValue: () => "12x12",
            description: "Grid size in format NxM (e.g., \"12x12\", \"24x24\")"
        );

        moveCommand.AddOption(coordinatesOption);
        moveCommand.AddOption(gridOption);

        moveCommand.SetHandler(
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

        return moveCommand;
    }

    public async Task SendAsync(Coordinates coordinates) =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name, coordinates);

}