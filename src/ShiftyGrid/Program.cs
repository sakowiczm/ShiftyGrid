using System.CommandLine;
using ShiftyGrid.Commands;
using ShiftyGrid.Common;

namespace ShiftyGrid;

internal class Program
{
    private static int Main(string[] args)
    {
        ConsoleManager.Attach();
    
        return CreateRootCommand().Invoke(args);
    }

    private static RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("ShiftyGrid - Window Management Utility");

        rootCommand.AddCommand(StartCommand.Create());
        rootCommand.AddCommand(new ExitCommand().Create());
        rootCommand.AddCommand(new SendMessageCommand().Create());
        rootCommand.AddCommand(new StatusCommand().Create());
        rootCommand.AddCommand(new AboutCommand().Create());

        rootCommand.SetHandler(() => StartCommand.Execute("info"));

        return rootCommand;
    }    
}