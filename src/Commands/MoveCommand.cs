using System.CommandLine;

using ShiftyGrid.Server;

namespace ShiftyGrid.Commands;

public class MoveCommand : BaseCommand
{
    public const string Name = "move";
    
    public Command Create()
    {
        var moveCommand = new Command(Name, "Send move command to running instance");
        moveCommand.SetHandler(() => Send());

        return moveCommand;
    }

    private void Send()
    {
        SendRequest(
            "Sending exit command to running instance...",
            new Request { Command = Name }
        );        
    }
}