using ShiftyGrid.Configuration;
using ShiftyGrid.Infrastructure.Models;
using System.CommandLine;

namespace ShiftyGrid.Operations.Commands;

public class MoveCommand : BaseCommand
{
    public const string Name = "move";

    public Command Create(Option<string?> configOption)
    {
        var moveCommand = new Command(Name, "Move the foreground window to specified grid coordinates");

        var coordinatesOption = new Option<string>(
            aliases: ["--coordinates", "-c"],
            description: "Coordinates: startX,startY,endX,endY (e.g., \"0,0,6,12\")"
        )
        { IsRequired = true };

        var gridOption = new Option<string?>(
            aliases: ["--grid", "-g"],
            description: "Grid size in format NxM (e.g., \"12x12\", \"24x24\")"
        );

        moveCommand.AddOption(coordinatesOption);
        moveCommand.AddOption(gridOption);

        moveCommand.SetHandler(async (context) =>
        {
            var coordinatesStr = context.ParseResult.GetValueForOption(coordinatesOption)!;
            var gridStr = context.ParseResult.GetValueForOption(gridOption);
            var configPath = context.ParseResult.GetValueForOption(configOption);

            if (gridStr == null)
            {
                var config = ConfigurationService.LoadConfiguration(configPath);
                gridStr = config.General.Grid ?? "12x12";
            }

            try
            {
                var coordinates = Coordinates.Parse(coordinatesStr, gridStr);
                await SendAsync(coordinates);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        });

        return moveCommand;
    }

    public async Task SendAsync(Coordinates coordinates) =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name, coordinates);

}