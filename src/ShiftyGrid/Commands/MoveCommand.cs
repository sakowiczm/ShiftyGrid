using ShiftyGrid.Configuration;

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

    public void Send(Position position) =>
        SendRequest("Sending move command to running instance...", Name, position);
    
}