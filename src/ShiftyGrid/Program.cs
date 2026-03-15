using ShiftyGrid.Infrastructure;
using ShiftyGrid.Operations.Commands;
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
        var configOption = new Option<string?>(
            aliases: ["--config", "-c"],
            description: "Path to configuration file (default: config.yaml in executable directory)")
        {
            ArgumentHelpName = "path"
        };

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

        rootCommand.AddOption(configOption);
        rootCommand.AddOption(logsPathOption);
        rootCommand.AddOption(logLevelOption);

        rootCommand.SetHandler(StartCommand.Execute, configOption, logsPathOption, logLevelOption);

        rootCommand.AddCommand(StartCommand.Create());
        rootCommand.AddCommand(new ExitCommand().Create());
        rootCommand.AddCommand(new StatusCommand().Create());
        rootCommand.AddCommand(new AboutCommand().Create());

        rootCommand.AddCommand(new MoveCommand().Create());
        rootCommand.AddCommand(new ArrangeCommand().Create());
        rootCommand.AddCommand(new OrganizeCommand().Create());
        rootCommand.AddCommand(new SwapCommand().Create());
        rootCommand.AddCommand(new ResizeCommand().Create());
        rootCommand.AddCommand(new PromoteCommand().Create());
        rootCommand.AddCommand(new FocusCommand().Create());

        return rootCommand;
    }    
}