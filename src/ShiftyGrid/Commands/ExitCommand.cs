using System.CommandLine;

namespace ShiftyGrid.Commands;

public class ExitCommand : BaseCommand
{
    public const string Name = "exit";

    public Command Create()
    {
        var exitCommand = new Command(Name, "Send exit command to running instance");
        exitCommand.SetHandler(() => Send());

        return exitCommand;
    }

    private void Send()
        => SendRequest("Sending exit command to running instance...", Name);
}