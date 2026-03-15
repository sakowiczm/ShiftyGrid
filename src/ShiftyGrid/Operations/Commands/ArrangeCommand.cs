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

        arrangeCommand.AddOption(rowsOption);
        arrangeCommand.AddOption(colsOption);

        arrangeCommand.SetHandler(
            async (rows, cols) => await SendAsync(rows, cols),
            rowsOption,
            colsOption
        );

        return arrangeCommand;
    }

    private async Task SendAsync(int rows, int cols)
    {
        var options = new ArrangeOptions(rows, cols);
        await SendRequestAsync($"Arranging windows in {rows}x{cols} grid", Name, options);
    }
}
