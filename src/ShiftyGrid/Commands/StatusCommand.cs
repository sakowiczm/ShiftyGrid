using System.CommandLine;

namespace ShiftyGrid.Commands;

public class StatusCommand : BaseCommand
{
    public const string Name = "status";

    public Command Create()
    {
        var exitCommand = new Command(Name, "Send status command to running instance");
        exitCommand.SetHandler(async () => await SendAsync());

        return exitCommand;
    }

    private async Task SendAsync() => await SendRequestAsync("Sending status command to running instance...", Name);
}