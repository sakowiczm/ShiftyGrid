namespace ShiftyGrid.Keyboard.Models;

/// <summary>
/// Represents modifier keys that can be combined with regular keys to form keyboard shortcuts.
/// Uses bitwise flags to allow multiple modifiers to be combined efficiently.
/// </summary>
/// <remarks>
/// This enum uses powers of 2 (bit shifting) to allow combining multiple modifiers
/// using bitwise OR operations. For example: ModifierKeys.Control | ModifierKeys.Alt
/// represents pressing both Ctrl and Alt simultaneously.
/// </remarks>
[Flags]
public enum ModifierKeys
{
    /// <summary>
    /// No modifier keys pressed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Control (Ctrl) key modifier.
    /// Bit position: 0 (value: 1)
    /// </summary>
    Control = 1 << 0,

    /// <summary>
    /// Alt key modifier.
    /// Bit position: 1 (value: 2)
    /// </summary>
    Alt = 1 << 1,

    /// <summary>
    /// Shift key modifier.
    /// Bit position: 2 (value: 4)
    /// </summary>
    Shift = 1 << 2,

    /// <summary>
    /// Windows (Win) key modifier.
    /// Bit position: 3 (value: 8)
    /// </summary>
    Win = 1 << 3
}
