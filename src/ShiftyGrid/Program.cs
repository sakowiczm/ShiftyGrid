using ShiftyGrid.Commands;
using ShiftyGrid.Common;
using System.CommandLine;

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

        // the same options as start command

        // todo: add --config

        var logsPathOption = new Option<string?>(
            aliases: ["--logs", "-l"],
            description: "Directory for log files (default: executable directory). Can be relative or absolute path.")
        {
            ArgumentHelpName = "path"
        };

        var logLevelOption = new Option<string>(
            aliases: ["--log-level"],
            getDefaultValue: () => "info",
            description: "Log level: none, debug, info, warn, error");

        rootCommand.AddOption(logsPathOption);
        rootCommand.AddOption(logLevelOption);

        rootCommand.SetHandler(StartCommand.Execute, logsPathOption, logLevelOption);

        rootCommand.AddCommand(StartCommand.Create());
        rootCommand.AddCommand(new ExitCommand().Create());
        rootCommand.AddCommand(new SendMessageCommand().Create());
        rootCommand.AddCommand(new StatusCommand().Create());
        rootCommand.AddCommand(new AboutCommand().Create());

        return rootCommand;
    }    
}