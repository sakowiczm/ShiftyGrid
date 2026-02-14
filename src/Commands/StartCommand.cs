using System.CommandLine;
using Windows.Win32;
using Windows.Win32.Foundation;
using ShiftyGrid.Common;
using ShiftyGrid.Handlers;
using ShiftyGrid.Server;

namespace ShiftyGrid.Commands;

public static class StartCommand
{
    public static Command Create()
    {
        var startCommand = new Command("start", "Start the ShiftyGrid server instance");
        var noLogsOption = new Option<bool>(
            aliases: ["--no-logs"],
            description: "Disable file logging");

        startCommand.AddOption(noLogsOption);
        startCommand.SetHandler(Execute, noLogsOption);

        return startCommand;
    }
    
    private static IpcServer? _ipcServer;
    private static bool _shouldExit;
    private static uint _mainThreadId;

    /// <summary>
    /// Starts IPC server instance
    /// </summary>
    public static void Execute(bool disableLogging)
    {
        // Capture the main thread ID for exit signaling
        _mainThreadId = PInvoke.GetCurrentThreadId();

        Logger.Initialize(disableLogging: disableLogging);
        Logger.Info("ShiftyGrid Starting");
        Logger.Info($"OS Version: {Environment.OSVersion}");

        using var instanceManager = new InstanceManager();

        if (!instanceManager.IsSingleInstance())
        {
            Console.WriteLine("Another instance is already running.");
            Logger.Warning("Another instance is already running. Exiting.");
            Environment.Exit(1);
        }

        Console.WriteLine("ShiftyGrid server started. Use 'ShiftyGrid.exe exit' to stop.");
        Logger.Info("Starting IPC server...");

        // Create request handler registry with state management callbacks
        var handlerRegistry = new HandlerRegistry(
            setShouldExit: value => _shouldExit = value,
            getShouldExit: () => _shouldExit,
            _mainThreadId);

        // Create and start IPC server
        _ipcServer = new IpcServer(handlerRegistry.Handle);
        _ipcServer.Start();

        Logger.Info("IPC server started, entering message loop");

        // Message loop - continues until _shouldExit is set
        while (!_shouldExit && PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        Logger.Info("Shutting down...");
        _ipcServer?.Stop();
        _ipcServer?.Dispose();
    }    
}