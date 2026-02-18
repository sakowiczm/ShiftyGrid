using ShiftyGrid.Configuration;

namespace ShiftyGrid.Commands;

public class MoveCommand : BaseCommand
{
    public const string Name = "move";

    //public Command Create()
    //{
    //    var moveCommand = new Command(Name, "Send move command to running instance");
    //    moveCommand.SetHandler(async () => await SendAsync(Position.LeftTop));
    //    return moveCommand;
    //}

    public async Task SendAsync(Position position) =>
        await SendRequestAsync("Sending move command to running instance...", Name, position);

}