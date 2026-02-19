using ShiftyGrid.Keyboard.Detection;
using ShiftyGrid.Keyboard.Hooks;
using ShiftyGrid.Keyboard.Models;
using ShiftyGrid.Keyboard.Modes;

namespace ShiftyGrid.Keyboard;

public class KeyboardEngine : IDisposable
{
    private readonly LowLevelKeyboardHook _keyboardHook;
    private readonly ShortcutDetector _detector;
    private readonly ModeManager _modeManager;
    private readonly DoubleTapDetector _doubleTapDetector;
    private bool _isStarted;
    private readonly object _lock = new();

    public event EventHandler<KeyboardTriggeredEventArgs>? ShortcutTriggered;
    public event EventHandler<ModeEventArgs>? ModeEntered;
    public event EventHandler<ModeEventArgs>? ModeExited;

    public KeyboardEngine() : this(300)
    {
    }

    public KeyboardEngine(int doubleTapWindowMs)
    {
        _keyboardHook = new LowLevelKeyboardHook();
        _detector = new ShortcutDetector();
        _modeManager = new ModeManager();
        _doubleTapDetector = new DoubleTapDetector(doubleTapWindowMs);

        _keyboardHook.KeyDown += OnKeyDown;
        _modeManager.ModeEntered += (s, e) => ModeEntered?.Invoke(this, e);
        _modeManager.ModeExited += (s, e) => ModeExited?.Invoke(this, e);
        _doubleTapDetector.DoubleTapDetected += OnDoubleTapDetected;
    }

    public void RegisterShortcut(ShortcutDefinition shortcut)
    {
        _detector.RegisterShortcut(shortcut);
    }

    public void RegisterMode(ModeDefinition mode)
    {
        _modeManager.RegisterMode(mode);

        // Register mode shortcuts
        foreach (var shortcut in mode.Shortcuts)
        {
            var modeShortcut = new ShortcutDefinition(
                shortcut.Id,
                shortcut.KeyCombination,
                shortcut.ActionId,
                shortcut.Scope,
                shortcut.BlockKey)
            {
                ModeId = mode.Id,
                ExitMode = shortcut.ExitMode
            };
            _detector.RegisterShortcut(modeShortcut);
        }
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_isStarted)
                return;

            _keyboardHook.Install();
            _isStarted = true;
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isStarted)
                return;

            _keyboardHook.Uninstall();
            _isStarted = false;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Process key press for double-tap detection
        _doubleTapDetector.ProcessKeyPress(e.VirtualKeyCode);

        // Check for ESC to cancel mode
        if (e.VirtualKeyCode == 0x1B && _modeManager.IsInMode) // VK_ESCAPE
        {
            var mode = _modeManager.GetMode(_modeManager.CurrentModeId!);
            if (mode?.AllowEscape == true)
            {
                _modeManager.ExitMode(ModeExitReason.Cancelled);
                e.ShouldBlock = true;
                return;
            }
        }

        // Regular key combination matching
        var keyCombination = new KeyCombination(e.VirtualKeyCode, e.Modifiers);
        var matches = _detector.FindMatches(keyCombination, _modeManager.CurrentModeId);

        if (matches.Count > 0)
        {
            foreach (var shortcut in matches)
            {
                // Dispatch to thread pool to avoid blocking hook
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    ShortcutTriggered?.Invoke(this, new KeyboardTriggeredEventArgs(shortcut));
                });

                // Handle mode entry
                if (shortcut.ActionId.StartsWith("enter_mode:"))
                {
                    var modeId = shortcut.ActionId.Substring("enter_mode:".Length);
                    _modeManager.TryEnterMode(modeId);
                }
                // If in mode and executed a shortcut, optionally exit mode
                else if (_modeManager.IsInMode)
                {
                    // Reset timeout on each key press
                    _modeManager.ResetTimeout();

                    // Auto-exit mode if shortcut is configured to do so
                    if (shortcut.ExitMode)
                    {
                        _modeManager.ExitMode(ModeExitReason.Completed);
                    }
                }

                if (shortcut.BlockKey)
                {
                    e.ShouldBlock = true;
                }
            }
        }
    }

    private void OnDoubleTapDetected(object? sender, DoubleTapEventArgs e)
    {
        // Create a double-tap key combination without follow-up key
        var doubleTapCombo = new KeyCombination(0, ModifierKeys.None, true, e.VirtualKeyCode);
        var matches = _detector.FindMatches(doubleTapCombo, null);

        if (matches.Count > 0)
        {
            foreach (var shortcut in matches)
            {
                // Dispatch to thread pool
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    ShortcutTriggered?.Invoke(this, new KeyboardTriggeredEventArgs(shortcut));
                });

                // Handle mode entry for double-tap triggers
                if (shortcut.ActionId.StartsWith("enter_mode:"))
                {
                    var modeId = shortcut.ActionId.Substring("enter_mode:".Length);
                    _modeManager.TryEnterMode(modeId);
                }
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _keyboardHook?.Dispose();
        _modeManager?.Dispose();
    }
}

public class KeyboardTriggeredEventArgs : EventArgs
{
    public KeyboardTriggeredEventArgs(ShortcutDefinition shortcut)
    {
        Shortcut = shortcut;
    }

    public ShortcutDefinition Shortcut { get; }
}
