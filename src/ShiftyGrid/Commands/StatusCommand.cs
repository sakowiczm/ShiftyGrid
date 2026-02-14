using System.CommandLine;
using ShiftyGrid.Server;

namespace ShiftyGrid.Commands;

public class StatusCommand : BaseCommand
{
    public const string Name = "status";
    
    public Command Create()
    {
        var exitCommand = new Command(Name, "Send status command to running instance");
        exitCommand.SetHandler(() => Send());

        return exitCommand;
    }

    private void Send()
    {
        SendRequest(
            "Sending status command to running instance...",
            new Request { Command = Name }
        );
    }
}