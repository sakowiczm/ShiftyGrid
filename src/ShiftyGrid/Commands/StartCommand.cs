using ShiftyGrid.Common;
using ShiftyGrid.Configuration;
using ShiftyGrid.Handlers;
using ShiftyGrid.Keyboard;
using ShiftyGrid.Keyboard.Helpers;
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
    private static IpcClient? _ipcClient;
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
            Console.WriteLine("Server is already running.");
            Logger.Warning("Server is already running.");
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

        // Create shared IPC client for keyboard shortcuts
        _ipcClient = new IpcClient();

        // Initialize keyboard engine
        _keyboardEngine = new KeyboardEngine(300);
        _keyboardEngine.ShortcutTriggered += OnShortcutTriggered;

        // Register shortcuts
        KeyboardRegisterMoveMode();

        // Start keyboard engine (hook installed on main thread)
        _keyboardEngine.Start();
        Logger.Info("Keyboard engine started");

        Logger.Info("IPC server started, entering message loop");

        // Detach from console to prevent forced termination when console closes
        // All early validation and startup messages have been displayed
        if (ConsoleManager.IsAttached)
        {
            ConsoleManager.DetachFromConsole();
            Logger.Info("Detached from parent console");
        }

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

    private static void KeyboardRegisterMoveMode()
    {
        // todo: better handling id/action

        // Create move mode with real activation keys
        var moveMode = new ModeDefinition(
            id: "move_mode",
            name: "Move Mode",
            timeoutMs: 5000,
            allowEscape: true
        );

        // Add mode shortcuts
        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "move-mode-left-half",
            keyCombination: new KeyCombination(Keys.VK_1, ModifierKeys.None),
            actionId: "move-mode-left-half",
            scope: ShortcutScope.Global,
            blockKey: true,
            exitMode: true
        ));

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "move-mode-right-half",
            keyCombination: new KeyCombination(Keys.VK_2, ModifierKeys.None),
            actionId: "move-mode-right-half",
            scope: ShortcutScope.Global,
            blockKey: true,
            exitMode: true
        ));

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "move-mode-center",
            keyCombination: new KeyCombination(Keys.VK_S, ModifierKeys.None),
            actionId: "move-mode-center",
            scope: ShortcutScope.Global,
            blockKey: true,
            exitMode: true
        ));

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "move-mode-center-wide",
            keyCombination: new KeyCombination(Keys.VK_SPACE, ModifierKeys.None),
            actionId: "move-mode-center-wide",
            scope: ShortcutScope.Global,
            blockKey: true,
            exitMode: true
        ));

        moveMode.Shortcuts.Add(new ShortcutDefinition(
            id: "move-mode-full",
            keyCombination: new KeyCombination(Keys.VK_F, ModifierKeys.None),
            actionId: "move-mode-full",
            scope: ShortcutScope.Global,
            blockKey: true,
            exitMode: true
        ));

        // Create and register activation shortcut using factory method
        var activationShortcut = moveMode.CreateActivationShortcut(
                new KeyCombination(Keys.VK_D, ModifierKeys.Control | ModifierKeys.Shift)
            );

        _keyboardEngine!.RegisterShortcut(activationShortcut);

        // Register the mode with the engine
        _keyboardEngine.RegisterMode(moveMode);

        Console.WriteLine($"Registered mode: {moveMode.Name} ({moveMode.Shortcuts.Count} shortcuts)");
    }

    private static async void OnShortcutTriggered(object? sender, KeyboardTriggeredEventArgs e)
    {
        Logger.Info($"Shortcut triggered: {e.Shortcut.Id} (Action: {e.Shortcut.ActionId})");

        // todo: consider naming?Position reaname to Placement?
        // todo: better wiring of actions to commands

        try
        {
            switch (e.Shortcut.ActionId)
            {
                case "move-mode-left-half":
                    await SendMoveRequestAsync(Position.LeftHalf);
                    break;
                case "move-mode-right-half":
                    await SendMoveRequestAsync(Position.RightHalf);
                    break;
                case "move-mode-center":
                    await SendMoveRequestAsync(Position.Center);
                    break;
                case "move-mode-center-wide":
                    await SendMoveRequestAsync(Position.CenterWide);
                    break;
                case "move-mode-full":
                    await SendMoveRequestAsync(Position.Full);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Keyboard Action] Error executing {e.Shortcut.ActionId}: {ex.Message}");
        }
    }

    // We starting this with IpcServer - so in theory we don't need to check if server is running.
    // And in this case we don't need individual IpcClient for command.
    private static async Task SendMoveRequestAsync(Position position)
    {
        // todo: unify this with BaseCommand

        if (_ipcClient == null)
        {
            Logger.Error("[Keyboard Action] IPC client not initialized");
            return;
        }

        try
        {
            var request = new Request
            {
                Command = "move",
                Data = System.Text.Json.JsonSerializer.SerializeToElement(
                    position,
                    IpcJsonContext.Default.Position)
            };

            var response = await _ipcClient.SendRequestAsync(request);

            if (!response.Success)
            {
                Logger.Error($"[Keyboard Action] Move failed: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Keyboard Action] Error sending move request: {ex.Message}", ex);
        }
    }
}