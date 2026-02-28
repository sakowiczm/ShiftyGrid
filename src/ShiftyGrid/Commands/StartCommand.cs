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

        startCommand.AddOption(logsPathOption);
        startCommand.AddOption(logLevelOption);
        startCommand.SetHandler(Execute, logsPathOption, logLevelOption);

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
    public static void Execute(string? logPath, string logLevel)
    {
        // Capture the main thread ID for exit signaling
        _mainThreadId = PInvoke.GetCurrentThreadId();

        Logger.Initialize(logPath, Logger.GetLogLevel(logLevel));

        Logger.Info("ShiftyGrid Starting");
        Console.WriteLine("ShiftyGrid Starting");
        Logger.Info($"OS Version: {Environment.OSVersion}");
        Console.WriteLine($"OS Version: {Environment.OSVersion}");

        using var instanceManager = new InstanceManager();

        if (!instanceManager.IsSingleInstance())
        {
            Console.WriteLine("Server is already running.");
            Logger.Warning("Server is already running.");
            Environment.Exit(1);
        }

        Console.WriteLine("ShiftyGrid server started. Use 'ShiftyGrid.exe exit' to stop.");
        Logger.Info("Starting IPC server...");

        DpiManager.Enable();

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
        GridPositioningKeyboardShortucts();
        SwapWindowsKeyboardShortcuts();
        ResizeWindowsKeyboardShortcuts();
        ArrangeSplitWindowsKeyboardShortcuts();

        // Start keyboard engine (hook installed on main thread)
        _keyboardEngine.Start();
        Logger.Info("Keyboard engine started");
        Logger.Info("IPC server started, entering message loop");

        ConsoleManager.Detach();

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

    private static void SwapWindowsKeyboardShortcuts()
    {
        // todo: better handling id/action

        // Swap Left    Ctrl + Alt + Left Arrow
        // Swap Right   Ctrl + Alt + Right Arrow
        // Swap Up    Ctrl + Alt + Up Arrow
        // Swap Down   Ctrl + Alt + Down Arrow

        var swapLeft = new ShortcutDefinition(
            id: "swap-left",
            keyCombination: new KeyCombination(Keys.VK_LEFT, ModifierKeys.Control | ModifierKeys.Alt),
            actionId: "swap-left",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        var swapRight = new ShortcutDefinition(
            id: "swap-right",
            keyCombination: new KeyCombination(Keys.VK_RIGHT, ModifierKeys.Control | ModifierKeys.Alt),
            actionId: "swap-right",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        var swapUp = new ShortcutDefinition(
            id: "swap-up",
            keyCombination: new KeyCombination(Keys.VK_UP, ModifierKeys.Control | ModifierKeys.Alt),
            actionId: "swap-up",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        var swapDown = new ShortcutDefinition(
            id: "swap-down",
            keyCombination: new KeyCombination(Keys.VK_DOWN, ModifierKeys.Control | ModifierKeys.Alt),
            actionId: "swap-down",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        _keyboardEngine!.RegisterShortcut(swapLeft);
        _keyboardEngine!.RegisterShortcut(swapRight);
        _keyboardEngine!.RegisterShortcut(swapUp);
        _keyboardEngine!.RegisterShortcut(swapDown);
    }

    private static void GridPositioningKeyboardShortucts()
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

    private static void ResizeWindowsKeyboardShortcuts()
    {
        // Context-aware resize (Alt + Arrow) - expands toward neighbor or shrinks when at edge

        // Resize Left      Alt + Left Arrow
        // Resize Right     Alt + Right Arrow
        // Resize Up        Alt + Up Arrow
        // Resize Down      Alt + Down Arrow

        var expandLeft = new ShortcutDefinition(
            id: "resize-expand-left",
            keyCombination: new KeyCombination(Keys.VK_LEFT, ModifierKeys.Alt),
            actionId: "resize-expand-left",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        var expandRight = new ShortcutDefinition(
            id: "resize-expand-right",
            keyCombination: new KeyCombination(Keys.VK_RIGHT, ModifierKeys.Alt),
            actionId: "resize-expand-right",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        var expandUp = new ShortcutDefinition(
            id: "resize-expand-up",
            keyCombination: new KeyCombination(Keys.VK_UP, ModifierKeys.Alt),
            actionId: "resize-expand-up",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        var expandDown = new ShortcutDefinition(
            id: "resize-expand-down",
            keyCombination: new KeyCombination(Keys.VK_DOWN, ModifierKeys.Alt),
            actionId: "resize-expand-down",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        // todo: rename resize == expand?

        _keyboardEngine!.RegisterShortcut(expandLeft);
        _keyboardEngine!.RegisterShortcut(expandRight);
        _keyboardEngine!.RegisterShortcut(expandUp);
        _keyboardEngine!.RegisterShortcut(expandDown);

        // Explicit shrink (Shift + Alt + Arrow) - shrinks window in arrow direction
        // Shrink Left      Shift + Alt + Left Arrow   (shrinks from right, window moves/shrinks left)
        // Shrink Right     Shift + Alt + Right Arrow  (shrinks from left, window moves/shrinks right)
        // Shrink Up        Shift + Alt + Up Arrow     (shrinks from bottom, window moves/shrinks up)
        // Shrink Down      Shift + Alt + Down Arrow   (shrinks from top, window moves/shrinks down)

        var shrinkLeft = new ShortcutDefinition(
            id: "resize-shrink-left",
            keyCombination: new KeyCombination(Keys.VK_LEFT, ModifierKeys.Shift | ModifierKeys.Alt),
            actionId: "resize-shrink-left",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        var shrinkRight = new ShortcutDefinition(
            id: "resize-shrink-right",
            keyCombination: new KeyCombination(Keys.VK_RIGHT, ModifierKeys.Shift | ModifierKeys.Alt),
            actionId: "resize-shrink-right",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        var shrinkUp = new ShortcutDefinition(
            id: "resize-shrink-up",
            keyCombination: new KeyCombination(Keys.VK_UP, ModifierKeys.Shift | ModifierKeys.Alt),
            actionId: "resize-shrink-up",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        var shrinkDown = new ShortcutDefinition(
            id: "resize-shrink-down",
            keyCombination: new KeyCombination(Keys.VK_DOWN, ModifierKeys.Shift | ModifierKeys.Alt),
            actionId: "resize-shrink-down",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        _keyboardEngine!.RegisterShortcut(shrinkLeft);
        _keyboardEngine!.RegisterShortcut(shrinkRight);
        _keyboardEngine!.RegisterShortcut(shrinkUp);
        _keyboardEngine!.RegisterShortcut(shrinkDown);
    }

    private static void ArrangeSplitWindowsKeyboardShortcuts()
    {
        // Split-screen arrange (Ctrl + Alt + =) - positions two adjacent windows side by side
        var arrangeSplit = new ShortcutDefinition(
            id: "arrange-split",
            keyCombination: new KeyCombination(Keys.VK_OEM_PLUS, ModifierKeys.Control | ModifierKeys.Alt),
            actionId: "arrange-split",
            scope: ShortcutScope.Global,
            blockKey: true
        );

        _keyboardEngine!.RegisterShortcut(arrangeSplit);
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

                case "swap-left":
                    await SendSwapRequestAsync(Direction.Left);
                    break;
                case "swap-right":
                    await SendSwapRequestAsync(Direction.Right);
                    break;
                case "swap-up":
                    await SendSwapRequestAsync(Direction.Up);
                    break;
                case "swap-down":
                    await SendSwapRequestAsync(Direction.Down);
                    break;

                case "resize-expand-left":
                    await SendResizeRequestAsync(WindowResize.ExpandLeft);
                    break;
                case "resize-expand-right":
                    await SendResizeRequestAsync(WindowResize.ExpandRight);
                    break;
                case "resize-expand-up":
                    await SendResizeRequestAsync(WindowResize.ExpandUp);
                    break;
                case "resize-expand-down":
                    await SendResizeRequestAsync(WindowResize.ExpandDown);
                    break;

                case "resize-shrink-left":
                    await SendResizeRequestAsync(WindowResize.ShrinkRight); // Shrink from right (window gets smaller toward left)
                    break;
                case "resize-shrink-right":
                    await SendResizeRequestAsync(WindowResize.ShrinkLeft); // Shrink from left (window gets smaller toward right)
                    break;
                case "resize-shrink-up":
                    await SendResizeRequestAsync(WindowResize.ShrinkDown); // Shrink from bottom (window gets smaller toward top)
                    break;
                case "resize-shrink-down":
                    await SendResizeRequestAsync(WindowResize.ShrinkUp); // Shrink from top (window gets smaller toward bottom)
                    break;

                case "arrange-split":
                    await SendArrangeSplitRequestAsync();
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

    private static async Task SendSwapRequestAsync(Direction direction)
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
                Command = "swap",
                Data = System.Text.Json.JsonSerializer.SerializeToElement(
                    direction,
                    IpcJsonContext.Default.Direction)
            };

            var response = await _ipcClient.SendRequestAsync(request);

            if (!response.Success)
            {
                Logger.Error($"[Keyboard Action] Swap failed: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Keyboard Action] Error sending swap request: {ex.Message}", ex);
        }
    }

    private static async Task SendResizeRequestAsync(WindowResize resize)
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
                Command = "resize",
                Data = System.Text.Json.JsonSerializer.SerializeToElement(
                    resize,
                    IpcJsonContext.Default.Direction)
            };

            var response = await _ipcClient.SendRequestAsync(request);

            if (!response.Success)
            {
                Logger.Error($"[Keyboard Action] Resize failed: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Keyboard Action] Error sending resize request: {ex.Message}", ex);
        }
    }

    private static async Task SendArrangeSplitRequestAsync()
    {
        if (_ipcClient == null)
        {
            Logger.Error("[Keyboard Action] IPC client not initialized");
            return;
        }

        try
        {
            var request = new Request
            {
                Command = "arrange-split"
            };

            var response = await _ipcClient.SendRequestAsync(request);

            if (!response.Success)
            {
                Logger.Error($"[Keyboard Action] Arrange failed: {response.Message}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"[Keyboard Action] Error sending arrange request: {ex.Message}", ex);
        }
    }
}