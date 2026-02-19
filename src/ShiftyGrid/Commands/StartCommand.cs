using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Handlers;
using ShiftyGrid.Keyboard;
using ShiftyGrid.Keyboard.Models;
using ShiftyGrid.Server;
using System.CommandLine;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace ShiftyGrid.Commands;

public static class StartCommand
{
    public static Command Create()
    {
        var startCommand = new Command("start", "Start the ShiftyGrid server instance");

        var logLevelOption = new Option<string>(
            aliases: ["--log-level"],
            getDefaultValue: () => "info",
            description: "Console log level: none, debug, info, warn, error");

        startCommand.AddOption(logLevelOption);
        startCommand.SetHandler(Execute, logLevelOption);

        return startCommand;
    }

    private static IpcServer? _ipcServer;
    private static KeyboardEngine? _keyboardEngine;
    private static bool _shouldExit;
    private static uint _mainThreadId;

    /// <summary>
    /// Starts IPC server instance with integrated keyboard engine
    /// </summary>
    public static void Execute(string logLevel)
    {
        // Capture the main thread ID for exit signaling
        _mainThreadId = PInvoke.GetCurrentThreadId();

        Logger.Initialize(null, Logger.GetLogLevel(logLevel));

        Logger.Info("ShiftyGrid Starting");
        Logger.Info($"OS Version: {Environment.OSVersion}");

        using var instanceManager = new InstanceManager();

        if (!instanceManager.IsSingleInstance())
        {
            Console.WriteLine("Another instance is already running. Exiting.");
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

        // Initialize keyboard engine
        _keyboardEngine = new KeyboardEngine(300);
        _keyboardEngine.ShortcutTriggered += OnShortcutTriggered;

        // todo: vkey - definition
        // todo: ExitMode in constructor?
        // todo: actionId for mode predefined in RegisterMode? 
        // todo: id maybe just Guid?
        
        // Register shortcuts

        // Create move mode with real activation keys
        var moveMode = new ModeDefinition(
            id: "move_mode",
            name: "Move Mode",
            timeoutMs: 5000,
            allowEscape: true
        );

        // Add mode shortcuts
        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_1",
            keyCombination: new KeyCombination(0x31, ModifierKeys.None), // Key "1"
            actionId: "move-left-top",
            scope: ShortcutScope.Global,
            blockKey: true
        )
        { ExitMode = true });

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_2",
            keyCombination: new KeyCombination(0x32, ModifierKeys.None), // Key "2"
            actionId: "move-right-top",
            scope: ShortcutScope.Global,
            blockKey: true
        )
        { ExitMode = true });

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_3",
            keyCombination: new KeyCombination(0x33, ModifierKeys.None), // Key "3"
            actionId: "move-left-bottom",
            scope: ShortcutScope.Global,
            blockKey: true
        )
        { ExitMode = true });

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_4",
            keyCombination: new KeyCombination(0x34, ModifierKeys.None), // Key "4"
            actionId: "move-right-bottom",
            scope: ShortcutScope.Global,
            blockKey: true
        )
        { ExitMode = true });

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_q",
            keyCombination: new KeyCombination(0x51, ModifierKeys.None), // Key "q"
            actionId: "move-left-half",
            scope: ShortcutScope.Global,
            blockKey: true
        )
        { ExitMode = true });

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_w",
            keyCombination: new KeyCombination(0x57, ModifierKeys.None), // Key "w"
            actionId: "move-right-half",
            scope: ShortcutScope.Global,
            blockKey: true
        )
        { ExitMode = true });

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "ctrl_shift_s_s",
            keyCombination: new KeyCombination(0x53, ModifierKeys.None), // Key "s"
            actionId: "move-center",
            scope: ShortcutScope.Global,
            blockKey: true
        )
        { ExitMode = true });

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "ctrl_shift_s_space",
            keyCombination: new KeyCombination(0x20, ModifierKeys.None), // Key "space"
            actionId: "move-center-wide",
            scope: ShortcutScope.Global,
            blockKey: true
        )
        { ExitMode = true });

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "ctrl_shift_s_space",
            keyCombination: new KeyCombination(0x46, ModifierKeys.None), // Key "f"
            actionId: "move-full",
            scope: ShortcutScope.Global,
            blockKey: true
        )
        { ExitMode = true });

        // Create and register activation shortcut using factory method
        var activationShortcut = moveMode.CreateActivationShortcut(
                new KeyCombination(0x53, ModifierKeys.Control | ModifierKeys.Shift) // Ctrl+Shift+S
            );
        _keyboardEngine.RegisterShortcut(activationShortcut);

        // Register the mode with the engine
        _keyboardEngine.RegisterMode(moveMode);

        Console.WriteLine($"Registered mode: {moveMode.Name} ({moveMode.Shortcuts.Count} shortcuts)");








        // Start keyboard engine (hook installed on main thread)
        _keyboardEngine.Start();
        Logger.Info("Keyboard engine started");

        Logger.Info("IPC server started, entering message loop");

        // Message loop - continues until _shouldExit is set
        // This pump serves both IPC and keyboard events
        while (!_shouldExit && PInvoke.GetMessage(out var msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(msg);
            PInvoke.DispatchMessage(msg);
        }

        Logger.Info("Shutting down...");
        _keyboardEngine?.Stop();
        _keyboardEngine?.Dispose();
        _ipcServer?.Stop();
        _ipcServer?.Dispose();
    }

    private static async void OnShortcutTriggered(object? sender, KeyboardTriggeredEventArgs e)
    {
        Logger.Info($"Shortcut triggered: {e.Shortcut.Id} (Action: {e.Shortcut.ActionId})");

        // todo: execute action based on e.Shortcut.ActionId
        // todo: make better abstraction for different types of commands ipc commands - can be triggered by cli or keyboard shortcuts or something else (e.g sheduler?)

        // todo: Position reaname to Placement?

        try
        {
            switch (e.Shortcut.ActionId)
            {
                case "move-left-top":
                    await new MoveCommand().SendAsync(Position.LeftTop);
                    break;
                case "move-right-top":
                    await new MoveCommand().SendAsync(Position.RightTop);
                    break;
                case "move-left-bottom":
                    await new MoveCommand().SendAsync(Position.LeftBottom);
                    break;
                case "move-right-bottom":
                    await new MoveCommand().SendAsync(Position.RightBottom);
                    break;
                case "move-left-half":
                    await new MoveCommand().SendAsync(Position.LeftHalf);
                    break;
                case "move-right-half":
                    await new MoveCommand().SendAsync(Position.RightHalf);
                    break;
                case "move-center":
                    await new MoveCommand().SendAsync(Position.Center);
                    break;
                case "move-center-wide":
                    await new MoveCommand().SendAsync(Position.CenterWide);
                    break;
                case "move-full":
                    await new MoveCommand().SendAsync(Position.Full);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Keyboard Action] Error executing {e.Shortcut.ActionId}: {ex.Message}");
        }

    }
}