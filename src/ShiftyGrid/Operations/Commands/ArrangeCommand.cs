using ShiftyGrid.Operations.Handlers;
using System.CommandLine;

namespace ShiftyGrid.Operations.Commands;

public class ArrangeCommand : BaseCommand
{
    public const string Name = "arrange";

    public Command Create()
    {
        var arrangeCommand = new Command(Name, "Arrange windows in a grid layout");

        var rowsOption = new Option<int>(
            aliases: ["--rows", "-r"],
            description: "Number of rows (1-2)",
            getDefaultValue: () => 1
        );

        var colsOption = new Option<int>(
            aliases: ["--cols", "-c"],
            description: "Number of columns (1-4)",
            getDefaultValue: () => 2
        );

        var zoneOption = new Option<string?>(
            aliases: ["--zone", "-z"],
            description: "Limit arrangement to a zone in 12x12 grid coordinates: x1,y1,x2,y2 (e.g. 0,0,6,12)",
            getDefaultValue: () => null
        );

        arrangeCommand.AddOption(rowsOption);
        arrangeCommand.AddOption(colsOption);
        arrangeCommand.AddOption(zoneOption);

        arrangeCommand.SetHandler(
            async (rows, cols, zone) => await SendAsync(rows, cols, zone),
            rowsOption,
            colsOption,
            zoneOption
        );

        return arrangeCommand;
    }

    private async Task SendAsync(int rows, int cols, string? zone)
    {
        var options = new ArrangeOptions(rows, cols, zone);
        await SendRequestAsync($"Arranging windows in {rows}x{cols} grid", Name, options);
    }
}
