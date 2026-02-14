using System.CommandLine;
using ShiftyGrid.Configuration;
using ShiftyGrid.Server;

namespace ShiftyGrid.Commands;

public class MoveCommand : BaseCommand
{
    public const string Name = "move";
    
    //public Command Create()
    //{
    //    var moveCommand = new Command(Name, "Send move command to running instance");
    //    moveCommand.SetHandler(() => Send(Position.LeftTop));
    //    return moveCommand;
    //}

    public void Send(Position position)
    {
        SendRequest(
            "Sending exit command to running instance...",
            new Request<Position> { 
                Command = Name,
                Data = position
            }
        );
    }
}