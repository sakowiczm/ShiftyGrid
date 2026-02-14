using ShiftyGrid.Keyboard.Models;

namespace ShiftyGrid.Keyboard.Modes;

public class ModeManager : IDisposable
{
    private readonly Dictionary<string, ModeDefinition> _modes = new();
    private readonly object _lock = new();
    private ModeContext? _currentMode;
    private Timer? _timeoutTimer;

    public event EventHandler<ModeEventArgs>? ModeEntered;
    public event EventHandler<ModeEventArgs>? ModeExited;

    public bool IsInMode => _currentMode != null;

    public string? CurrentModeId => _currentMode?.Mode.Id;

    public void RegisterMode(ModeDefinition mode)
    {
        lock (_lock)
        {
            _modes[mode.Id] = mode;
        }
    }

    public void UnregisterMode(string modeId)
    {
        lock (_lock)
        {
            if (_currentMode?.Mode.Id == modeId)
            {
                ExitMode(ModeExitReason.Cancelled);
            }
            _modes.Remove(modeId);
        }
    }

    public ModeDefinition? GetMode(string modeId)
    {
        lock (_lock)
        {
            return _modes.TryGetValue(modeId, out var mode) ? mode : null;
        }
    }

    public bool TryEnterMode(string modeId)
    {
        lock (_lock)
        {
            if (!_modes.TryGetValue(modeId, out var mode))
            {
                return false;
            }

            // Exit current mode if any
            if (_currentMode != null)
            {
                ExitModeInternal(ModeExitReason.NewMode);
            }

            _currentMode = new ModeContext(mode, DateTime.UtcNow);

            // Start timeout timer if configured
            if (mode.TimeoutMs > 0)
            {
                _timeoutTimer = new Timer(
                    OnTimeout,
                    null,
                    mode.TimeoutMs,
                    Timeout.Infinite);
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                ModeEntered?.Invoke(this, new ModeEventArgs(mode, ModeExitReason.None));
            });

            return true;
        }
    }

    public void ExitMode(ModeExitReason reason)
    {
        lock (_lock)
        {
            ExitModeInternal(reason);
        }
    }

    private void ExitModeInternal(ModeExitReason reason)
    {
        if (_currentMode == null)
        {
            return;
        }

        var mode = _currentMode.Mode;
        _currentMode = null;

        _timeoutTimer?.Dispose();
        _timeoutTimer = null;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            ModeExited?.Invoke(this, new ModeEventArgs(mode, reason));
        });
    }

    private void OnTimeout(object? state)
    {
        lock (_lock)
        {
            if (_currentMode != null)
            {
                ExitModeInternal(ModeExitReason.Timeout);
            }
        }
    }

    public void ResetTimeout()
    {
        lock (_lock)
        {
            if (_currentMode != null && _currentMode.Mode.TimeoutMs > 0)
            {
                _timeoutTimer?.Dispose();
                _timeoutTimer = new Timer(
                    OnTimeout,
                    null,
                    _currentMode.Mode.TimeoutMs,
                    Timeout.Infinite);
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;
            _currentMode = null;
        }
    }
}

public class ModeContext
{
    public ModeContext(ModeDefinition mode, DateTime enteredAt)
    {
        Mode = mode;
        EnteredAt = enteredAt;
    }

    public ModeDefinition Mode { get; }
    public DateTime EnteredAt { get; }
}

public class ModeEventArgs : EventArgs
{
    public ModeEventArgs(ModeDefinition mode, ModeExitReason exitReason)
    {
        Mode = mode;
        ExitReason = exitReason;
    }

    public ModeDefinition Mode { get; }
    public ModeExitReason ExitReason { get; }
}

public enum ModeExitReason
{
    None,
    Timeout,
    Cancelled,
    NewMode
}
