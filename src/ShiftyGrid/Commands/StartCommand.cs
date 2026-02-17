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
        var noLogsOption = new Option<bool>(
            aliases: ["--no-logs"],
            description: "Disable file logging");

        startCommand.AddOption(noLogsOption);
        startCommand.SetHandler(Execute, noLogsOption);

        return startCommand;
    }
    
    private static IpcServer? _ipcServer;
    private static KeyboardEngine? _keyboardEngine;
    private static bool _shouldExit;
    private static uint _mainThreadId;

    /// <summary>
    /// Starts IPC server instance with integrated keyboard engine
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

        // Initialize keyboard engine
        _keyboardEngine = new KeyboardEngine(300);
        _keyboardEngine.ShortcutTriggered += OnShortcutTriggered;

        //// Register shortcuts
        //var dblCtrl = new ShortcutDefinition(
        //    id: "move_1",
        //    keyCombination: new KeyCombination(0, ModifierKeys.None, true, 0xA2), // 2xCtrl
        //    actionId: "move",
        //    scope: ShortcutScope.Global,
        //    blockKey: true
        //);
        //_keyboardEngine.RegisterShortcut(dblCtrl);



        var dblCtrlMode = new ModeDefinition(
            id: "dbl_ctrl_mode",
            name: "Double CTRL Mode",
            activationKeys: new KeyCombination(0, ModifierKeys.None, true, 0xA2), // Double-tap left CTRL (0xA2)
            timeoutMs: 5000,
            allowEscape: true
        );

        // Add mode shortcuts for 1, 2, 3
        dblCtrlMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_1",
            keyCombination: new KeyCombination(0x31, ModifierKeys.None), // Key "1"
            actionId: "move-left-top",
            scope: ShortcutScope.Global,
            blockKey: true
        ));

        dblCtrlMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_2",
            keyCombination: new KeyCombination(0x32, ModifierKeys.None), // Key "2"
            actionId: "move-right-top",
            scope: ShortcutScope.Global,
            blockKey: true
        ));

        dblCtrlMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_3",
            keyCombination: new KeyCombination(0x33, ModifierKeys.None), // Key "3"
            actionId: "move-left-bottom",
            scope: ShortcutScope.Global,
            blockKey: true
        ));

        dblCtrlMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_4",
            keyCombination: new KeyCombination(0x34, ModifierKeys.None), // Key "4"
            actionId: "move-right-bottom",
            scope: ShortcutScope.Global,
            blockKey: true
        ));

        dblCtrlMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_q",
            keyCombination: new KeyCombination(0x51, ModifierKeys.None), // Key "q"
            actionId: "move-left-half",
            scope: ShortcutScope.Global,
            blockKey: true
        ));

        dblCtrlMode.Shortcuts.Add(new ShortcutDefinition(
            id: "dbl_ctrl_w",
            keyCombination: new KeyCombination(0x57, ModifierKeys.None), // Key "w"
            actionId: "move-right-half",
            scope: ShortcutScope.Global,
            blockKey: true
        ));


        // Register the mode with the engine
        _keyboardEngine.RegisterMode(dblCtrlMode);

        Console.WriteLine($"Registered mode: {dblCtrlMode.Name} ({dblCtrlMode.Shortcuts.Count} shortcuts)");







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

    private static void OnShortcutTriggered(object? sender, KeyboardTriggeredEventArgs e)
    {
        Logger.Info($"Shortcut triggered: {e.Shortcut.Id} (Action: {e.Shortcut.ActionId})");

        // todo: execute action based on e.Shortcut.ActionId
        // todo: make better abstraction for different types of commands ipc commands - can be triggered by cli or keyboard shortcuts or something else (e.g sheduler?)

        try
        {
            switch (e.Shortcut.ActionId)
            {
                case "move-left-top":
                    new MoveCommand().Send(Position.LeftTop);
                    break;
                case "move-right-top":
                    new MoveCommand().Send(Position.RightTop);
                    break;
                case "move-left-bottom":
                    new MoveCommand().Send(Position.LeftBottom);
                    break;
                case "move-right-bottom":
                    new MoveCommand().Send(Position.RightBottom);
                    break;
                case "move-left-half":
                    new MoveCommand().Send(Position.LeftHalf);
                    break;
                case "move-right-half":
                    new MoveCommand().Send(Position.RightHalf);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Keyboard Action] Error executing {e.Shortcut.ActionId}: {ex.Message}");
            Console.WriteLine($"[Keyboard Action] Error executing {e.Shortcut.ActionId}: {ex.Message}");
        }


    }
}