using System.CommandLine;

namespace ShiftyGrid.Commands;

public class ExitCommand : BaseCommand
{
    public const string Name = "exit";

    public Command Create()
    {
        var exitCommand = new Command(Name, "Send exit command to running instance");
        exitCommand.SetHandler(async () => await SendAsync());

        return exitCommand;
    }

    private async Task SendAsync()
        => await SendRequestAsync("Sending exit command to running instance...", Name);
}