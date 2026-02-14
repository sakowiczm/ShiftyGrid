using System.Collections.Concurrent;

namespace ShiftyGrid.Keyboard.Detection;

/// <summary>
/// Detects double-tap patterns on modifier keys (SHIFT, CTRL, ALT, WIN).
/// Tracks the timing of key presses to identify when a key is tapped twice in quick succession.
/// </summary>
/// <remarks>
/// <para>
/// The detector monitors individual modifier keys (including left/right variants) and fires
/// events when a double-tap is detected within the configured time window (default: 300ms).
/// </para>
/// <para><strong>Supported Keys:</strong></para>
/// <list type="bullet">
/// <item>SHIFT (both left and right, or separately)</item>
/// <item>CTRL (both left and right, or separately)</item>
/// <item>ALT (both left and right, or separately)</item>
/// <item>WIN (both left and right, or separately)</item>
/// </list>
/// <para><strong>Thread Safety:</strong></para>
/// <para>
/// This class is thread-safe and can be accessed from multiple threads simultaneously.
/// Uses ConcurrentDictionary for lock-free tracking of key press times.
/// </para>
/// </remarks>
public class DoubleTapDetector
{
    /// <summary>
    /// Virtual key codes for modifier keys (left and right variants).
    /// </summary>
    private static class VK
    {
        public const int LSHIFT = 0xA0;
        public const int RSHIFT = 0xA1;
        public const int LCONTROL = 0xA2;
        public const int RCONTROL = 0xA3;
        public const int LALT = 0xA4;
        public const int RALT = 0xA5;
        public const int LWIN = 0x5B;
        public const int RWIN = 0x5C;
        public const int SHIFT = 0x10;   // Generic SHIFT
        public const int CONTROL = 0x11; // Generic CONTROL
        public const int ALT = 0x12;     // Generic ALT
    }

    private readonly int _doubleTapWindowMs;
    private readonly ConcurrentDictionary<int, long> _lastKeyPressTimes;
    private readonly object _lock = new();

    /// <summary>
    /// Occurs when a double-tap is detected on a modifier key.
    /// </summary>
    public event EventHandler<DoubleTapEventArgs>? DoubleTapDetected;

    /// <summary>
    /// Initializes a new instance of the <see cref="DoubleTapDetector"/> class.
    /// </summary>
    /// <param name="doubleTapWindowMs">
    /// The maximum time window in milliseconds between two key presses to be considered a double-tap.
    /// Default is 300ms (standard double-click timing).
    /// </param>
    public DoubleTapDetector(int doubleTapWindowMs = 300)
    {
        _doubleTapWindowMs = doubleTapWindowMs;
        _lastKeyPressTimes = new ConcurrentDictionary<int, long>();
    }

    /// <summary>
    /// Processes a key press event and detects double-tap patterns.
    /// </summary>
    /// <param name="virtualKeyCode">The Windows virtual key code of the pressed key.</param>
    /// <remarks>
    /// This method should be called for every key press event.
    /// It tracks timing and fires DoubleTapDetected when a double-tap is identified.
    /// </remarks>
    public void ProcessKeyPress(int virtualKeyCode)
    {
        // Only process modifier keys
        if (!IsModifierKey(virtualKeyCode))
            return;

        var now = Environment.TickCount64;

        // Get the last press time for this key
        if (_lastKeyPressTimes.TryGetValue(virtualKeyCode, out var lastPressTime))
        {
            var timeSinceLastPress = now - lastPressTime;

            if (timeSinceLastPress <= _doubleTapWindowMs)
            {
                // Double-tap detected!
                _lastKeyPressTimes.TryRemove(virtualKeyCode, out _);

                // Raise event
                var eventArgs = new DoubleTapEventArgs(virtualKeyCode);
                DoubleTapDetected?.Invoke(this, eventArgs);
                return;
            }
        }

        // Update the last press time
        _lastKeyPressTimes[virtualKeyCode] = now;
    }

    /// <summary>
    /// Resets the tracking state for all keys.
    /// Useful when entering/exiting modes or resetting the detector state.
    /// </summary>
    public void Reset()
    {
        _lastKeyPressTimes.Clear();
    }

    /// <summary>
    /// Determines if the specified virtual key code is a modifier key that supports double-tap.
    /// </summary>
    /// <param name="virtualKeyCode">The virtual key code to check.</param>
    /// <returns>True if the key is a supported modifier key; otherwise, false.</returns>
    private static bool IsModifierKey(int virtualKeyCode)
    {
        return virtualKeyCode switch
        {
            VK.LSHIFT or VK.RSHIFT or VK.SHIFT => true,
            VK.LCONTROL or VK.RCONTROL or VK.CONTROL => true,
            VK.LALT or VK.RALT or VK.ALT => true,
            VK.LWIN or VK.RWIN => true,
            _ => false
        };
    }

    /// <summary>
    /// Maps a generic modifier key to its left variant.
    /// Used to normalize key codes for matching.
    /// </summary>
    public static int MapToLeftVariant(int virtualKeyCode)
    {
        return virtualKeyCode switch
        {
            VK.SHIFT => VK.LSHIFT,
            VK.CONTROL => VK.LCONTROL,
            VK.ALT => VK.LALT,
            _ => virtualKeyCode
        };
    }

    /// <summary>
    /// Maps a generic modifier key to its right variant.
    /// Used to normalize key codes for matching.
    /// </summary>
    public static int MapToRightVariant(int virtualKeyCode)
    {
        return virtualKeyCode switch
        {
            VK.SHIFT => VK.RSHIFT,
            VK.CONTROL => VK.RCONTROL,
            VK.ALT => VK.RALT,
            _ => virtualKeyCode
        };
    }

    /// <summary>
    /// Gets the generic modifier type from a specific left/right variant.
    /// </summary>
    public static int GetGenericModifier(int virtualKeyCode)
    {
        return virtualKeyCode switch
        {
            VK.LSHIFT or VK.RSHIFT => VK.SHIFT,
            VK.LCONTROL or VK.RCONTROL => VK.CONTROL,
            VK.LALT or VK.RALT => VK.ALT,
            _ => virtualKeyCode
        };
    }
}

/// <summary>
/// Event arguments for double-tap detection events.
/// </summary>
public class DoubleTapEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DoubleTapEventArgs"/> class.
    /// </summary>
    /// <param name="virtualKeyCode">The virtual key code of the double-tapped key.</param>
    public DoubleTapEventArgs(int virtualKeyCode)
    {
        VirtualKeyCode = virtualKeyCode;
    }

    /// <summary>
    /// Gets the virtual key code of the double-tapped key.
    /// </summary>
    public int VirtualKeyCode { get; }
}
