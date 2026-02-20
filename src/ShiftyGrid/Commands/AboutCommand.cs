using System.CommandLine;

namespace ShiftyGrid.Commands;

public class AboutCommand
{
    public const string Name = "about";
    private const string Version = "0.1.0";
    private const string RepositoryUrl = "https://github.com/sakowiczm/ShiftyGrid";

    public Command Create()
    {
        var aboutCommand = new Command(Name, "About ShiftyGrid");
        aboutCommand.SetHandler(Execute);

        return aboutCommand;
    }

    // no command handler is this case - those are only for communication with IpcServer
    // don't need to check if IpcServer is running

    private static void Execute()
    {
        Console.WriteLine();
        Console.WriteLine("================================================================");
        Console.WriteLine("                         ShiftyGrid                             ");
        Console.WriteLine("================================================================");
        Console.WriteLine();
        Console.WriteLine($"  Version:        {Version}");
        Console.WriteLine("  Description:    A high-performance, keyboard-driven window");
        Console.WriteLine("                  manager for Windows with modal shortcuts and");
        Console.WriteLine("                  grid-based positioning.");
        Console.WriteLine();
        Console.WriteLine("  Features:");
        Console.WriteLine("    - Modal keyboard shortcuts (Ctrl+Shift+S)");
        Console.WriteLine("    - Grid-based window positioning");
        Console.WriteLine("    - Multi-monitor support");
        Console.WriteLine("    - Native AOT compilation for fast startup");
        Console.WriteLine();
        Console.WriteLine($"  Repository:     {RepositoryUrl}");
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("  For more information, visit the repository or run with --help");
        Console.WriteLine();
    }
}
