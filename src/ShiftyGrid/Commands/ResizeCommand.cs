using ShiftyGrid.Common;
using ShiftyGrid.Handlers;
using ShiftyGrid.Server;
using System.CommandLine;

namespace ShiftyGrid.Commands;

internal class ResizeCommand : BaseCommand
{
    public const string Name = "resize";

    //public static Command Create()
    //{
    //    var resizeCommand = new Command(Name, "Resize the foreground window");

    //    var directionArgument = new Argument<string>(
    //        name: "direction",
    //        description: "Direction to resize: left, right, up, down")
    //    {
    //        Arity = ArgumentArity.ExactlyOne
    //    };

    //    resizeCommand.AddArgument(directionArgument);
    //    resizeCommand.SetHandler(Execute, directionArgument);

    //    return resizeCommand;
    //}

    public async Task Execute(Direction? direction)
    {
        //if (string.IsNullOrEmpty(direction)) 
        //{
        //    Logger.Debug("Ivalid direction.");
        //    return;
        //}

        //Direction? resizeDirection = direction.ToLowerInvariant() switch
        //{
        //    "left" => Direction.Left,
        //    "right" => Direction.Right,
        //    "up" => Direction.Up,
        //    "down" => Direction.Down,
        //    _ => null
        //};


        await SendRequestAsync($"Resizing window {direction}", Name, direction);
    }
}
