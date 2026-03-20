using ShiftyGrid.Configuration;
using ShiftyGrid.Keyboard;
using ShiftyGrid.Server;
using ShiftyGrid.Windows;
using System.CommandLine;
using Windows.Win32;
using Windows.Win32.Foundation;
using ShiftyGrid.Infrastructure;
using ShiftyGrid.Infrastructure.Display;
using ShiftyGrid.Operations.Handlers;
using ShiftyGrid.Common;

namespace ShiftyGrid.Operations.Commands;

public static class StartCommand
{
    public static Command Create()
    {
        var startCommand = new Command("start", "Start the ShiftyGrid server instance");

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

        startCommand.AddOption(configOption);
        startCommand.AddOption(logsPathOption);
        startCommand.AddOption(logLevelOption);
        startCommand.SetHandler(Execute, configOption, logsPathOption, logLevelOption);

        return startCommand;
    }

    private static IpcServer? _ipcServer;
    //private static IpcClient? _ipcClient;
    private static KeyboardEngine? _keyboardEngine;
    private static WindowLifecycleMonitor? _WindowLifecycleMonitor;
    private static bool _shouldExit;
    private static uint _mainThreadId;


    // Public properties to expose startup parameters for reload
    public static string? ConfigPath { get; private set; }
    public static string? LogPath { get; private set; }
    public static string LogLevel { get; private set; } = "info";

    /// <summary>
    /// Starts IPC server instance with integrated keyboard engine
    /// </summary>
    public static void Execute(string? configPath, string? logPath, string logLevel)
    {
        // Capture the main thread ID for exit signaling
        _mainThreadId = PInvoke.GetCurrentThreadId();

        // Store parameters for reload
        ConfigPath = configPath;
        LogPath = logPath;
        LogLevel = logLevel;

        // Check for single instance
        using var instanceManager = new InstanceManager();
        if (!instanceManager.IsSingleInstance())
        {
            Console.WriteLine("Server is already running.");
            Logger.Warning("Server is already running.");
            Environment.Exit(1);
        }

        _shouldExit = false;

        Logger.Initialize(LogPath, Logger.GetLogLevel(LogLevel));

        Logger.Info("ShiftyGrid Starting");
        Console.WriteLine("ShiftyGrid Starting");
        Logger.Info($"OS Version: {Environment.OSVersion}");
        Console.WriteLine($"OS Version: {Environment.OSVersion}");

        // Load configuration
        ShiftyGridConfig config;
        try
        {
            config = ConfigurationService.LoadConfiguration(ConfigPath);
            Console.WriteLine($"Configuration loaded from {ConfigurationService.ResolveConfigPath(ConfigPath)}");
        }
        catch (ConfigurationException ex)
        {
            Logger.Error($"Configuration error: {ex.Message}");
            Console.WriteLine($"ERROR: Configuration error: {ex.Message}");
            Console.WriteLine("Fix the configuration file and try again.");
            Environment.Exit(1);
            return;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load configuration: {ex.Message}", ex);
            Console.WriteLine($"ERROR: Failed to load configuration: {ex.Message}");
            Environment.Exit(1);
            return;
        }

        Console.WriteLine("ShiftyGrid server started. Use 'ShiftyGrid.exe exit' to stop.");
        Logger.Info("Starting IPC server...");

        DpiManager.Enable();

        // Create request handler registry with state management callbacks
        var handlerRegistry = new HandlerRegistry(
            setShouldExit: value => _shouldExit = value,
            getShouldExit: () => _shouldExit,
            _mainThreadId,
            config);

        // Create and start IPC server
        _ipcServer = new IpcServer(handlerRegistry.Handle);
        _ipcServer.Start();

        // Create shared IPC client for keyboard shortcuts
        //_ipcClient = new IpcClient();

        // Initialize keyboard engine
        _keyboardEngine = new KeyboardEngine(300);

        // Register shortcuts from configuration
        var shortcutRegistrar = new ShortcutRegistrar(_keyboardEngine, config);
        _keyboardEngine.ShortcutTriggered += shortcutRegistrar.OnShortcutTriggered;
        shortcutRegistrar.RegisterAll();

        // Start keyboard engine (hook installed on main thread)
        _keyboardEngine.Start();
        Logger.Info("Keyboard engine started");
        Logger.Info("IPC server started, entering message loop");

        // Initialize window event monitor for auto-organization
        if (config.General.AutoOrganize)
        {
            var windowMatcher = new WindowMatcher(config);
            var windowOrganizer = new WindowOrganizer(windowMatcher, config.General.Gap);
            _WindowLifecycleMonitor = new WindowLifecycleMonitor(windowOrganizer);
            _WindowLifecycleMonitor.Start();
            Logger.Info("Window event monitor started (auto-organize enabled)");
        }

        // Run startup commands
        if (config.Startup.Commands.Count > 0)
        {
            Logger.Info("Running startup commands...");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (var commandString in config.Startup.Commands)
                {
                    Logger.Info($"Running startup command: {commandString}");
                    ProcessSpawner.SpawnCommand(commandString);
                }
            });
        }

        ConsoleManager.Detach();

        // Message loop - continues until _shouldExit is set
        // This pump serves both IPC and keyboard events
        while (!_shouldExit && PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        Logger.Info("Shutting down...");
        _WindowLifecycleMonitor?.Stop();
        _WindowLifecycleMonitor?.Dispose();
        _keyboardEngine?.Stop();
        _keyboardEngine?.Dispose();
        _ipcServer?.Stop();
        _ipcServer?.Dispose();
    }
}
