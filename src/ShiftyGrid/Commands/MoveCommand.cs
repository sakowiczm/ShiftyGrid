using ShiftyGrid.Configuration;
using System.CommandLine;

namespace ShiftyGrid.Commands;

public class MoveCommand : BaseCommand
{
    public const string Name = "move";

    public Command Create()
    {
        var moveCommand = new Command(Name, "Move the foreground window to specified grid position");

        var positionOption = new Option<string>(
            aliases: ["--position", "-p"],
            description: "Position coordinates: startX,startY,endX,endY (e.g., \"0,0,6,12\")"
        )
        { IsRequired = true };

        var gridOption = new Option<string>(
            aliases: ["--grid", "-g"],
            getDefaultValue: () => "12x12",
            description: "Grid size in format NxM (e.g., \"12x12\", \"24x24\")"
        );

        moveCommand.AddOption(positionOption);
        moveCommand.AddOption(gridOption);

        moveCommand.SetHandler(
            async (positionStr, gridStr) =>
            {
                try
                {
                    var position = GridParser.ParsePosition(positionStr, gridStr);
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

        return moveCommand;
    }

    public async Task SendAsync(Position position) =>
        await SendRequestAsync($"Sending {Name} command to running instance...", Name, position);

}