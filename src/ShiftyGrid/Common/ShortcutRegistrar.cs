using ShiftyGrid.Keyboard.Models;
using ShiftyGrid.Configuration;
using ShiftyGrid.Keyboard;
using ShiftyGrid.Common;

namespace ShiftyGrid.Infrastructure;

/// <summary>
/// Dynamically registers keyboard shortcuts from configuration
/// </summary>
public class ShortcutRegistrar
{
    private readonly KeyboardEngine _keyboardEngine;
    private readonly ShiftyGridConfig _config;

    public ShortcutRegistrar(KeyboardEngine keyboardEngine, ShiftyGridConfig config)
    {
        _keyboardEngine = keyboardEngine ?? throw new ArgumentNullException(nameof(keyboardEngine));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void RegisterAll()
    {
        Logger.Info("Registering keyboard shortcuts from configuration...");

        // Register global shortcuts
        int shortcutCount = 0;
        foreach (var shortcutConfig in _config.Keyboard.Shortcuts)
        {
            RegisterShortcut(shortcutConfig);
            shortcutCount += shortcutConfig.Bindings.Count;
        }

        Logger.Info($"Registered {shortcutCount} global shortcuts");

        // Register modal shortcuts
        int modeCount = 0;
        foreach (var modeConfig in _config.Keyboard.Modes)
        {
            RegisterMode(modeConfig);
            modeCount++;
        }

        Logger.Info($"Registered {modeCount} modal modes");
    }

    private void RegisterShortcut(ShortcutConfig shortcutConfig)
    {
        foreach (var binding in shortcutConfig.Bindings)
        {
            try
            {
                var keyCombination = ShortcutParser.Parse(binding);
                var shortcutDef = new ShortcutDefinition(
                    id: $"shortcut-{binding}",
                    keyCombination: keyCombination,
                    actionId: shortcutConfig.Command, // Store command string as actionId
                    scope: ShortcutScope.Global,
                    blockKey: shortcutConfig.BlockKey
                );

                _keyboardEngine.RegisterShortcut(shortcutDef);
                Logger.Debug($"Registered shortcut: {binding} -> {shortcutConfig.Command}");
            }
            catch (ConfigurationException ex)
            {
                Logger.Error($"Failed to register shortcut '{binding}': {ex.Message}");
                throw;
            }
        }
    }

    private void RegisterMode(ModeConfig modeConfig)
    {
        try
        {
            var mode = new ModeDefinition(
                id: modeConfig.Id,
                name: modeConfig.Name,
                timeoutMs: modeConfig.TimeoutMs,
                allowEscape: modeConfig.AllowEscape
            );

            // Register mode shortcuts
            foreach (var shortcutConfig in modeConfig.Shortcuts)
            {
                foreach (var binding in shortcutConfig.Bindings)
                {
                    var keyCombination = ShortcutParser.Parse(binding);
                    var shortcutDef = new ShortcutDefinition(
                        id: $"mode-{modeConfig.Id}-{binding}",
                        keyCombination: keyCombination,
                        actionId: shortcutConfig.Command, // Store command string as actionId
                        scope: ShortcutScope.Global,
                        blockKey: shortcutConfig.BlockKey,
                        exitMode: shortcutConfig.ExitMode
                    );

                    mode.Shortcuts.Add(shortcutDef);
                    Logger.Debug($"Added mode shortcut to '{modeConfig.Id}': {binding} -> {shortcutConfig.Command}");
                }
            }

            // Register the mode with the keyboard engine
            _keyboardEngine.RegisterMode(mode);

            // Register mode activation shortcuts
            foreach (var binding in modeConfig.Activation)
            {
                var keyCombination = ShortcutParser.Parse(binding);
                var activationShortcut = mode.CreateActivationShortcut(keyCombination);
                _keyboardEngine.RegisterShortcut(activationShortcut);
                Logger.Debug($"Registered mode activation: {binding} -> {modeConfig.Name}");
            }

            Logger.Info($"Registered mode '{modeConfig.Name}' with {mode.Shortcuts.Count} shortcuts");
        }
        catch (ConfigurationException ex)
        {
            Logger.Error($"Failed to register mode '{modeConfig.Id}': {ex.Message}");
            throw;
        }
    }

    public void OnShortcutTriggered(object? sender, KeyboardTriggeredEventArgs e)
    {
        var commandString = e.Shortcut.ActionId;

        // Skip mode activation commands (handled internally by keyboard engine)
        if (commandString.StartsWith("activate-mode-", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Logger.Debug($"Shortcut triggered: {e.Shortcut.Id} -> {commandString}");

        // Execute command via spawned CLI process (fire-and-forget)
        _ = Task.Run(() =>
        {
            try
            {
                ProcessSpawner.SpawnCommand(commandString);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error spawning command '{commandString}': {ex.Message}");
            }
        });
    }
}
