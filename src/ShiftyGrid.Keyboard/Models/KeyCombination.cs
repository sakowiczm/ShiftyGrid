namespace ShiftyGrid.Keyboard.Models;

/// <summary>
/// Represents a keyboard shortcut combination consisting of a main key and optional modifier keys.
/// This is an immutable value type (readonly struct) designed for efficient comparison and storage.
/// </summary>
/// <remarks>
/// <para>
/// A KeyCombination is the core building block for keyboard shortcuts. It combines:
/// - A virtual key code (the main key, e.g., 'N' key = 0x4E)
/// - Zero or more modifier keys (Ctrl, Alt, Shift, Win)
/// - Optional double-tap trigger (e.g., 2xSHIFT)
/// </para>
/// <para>
/// Examples:
/// - Ctrl+N: VirtualKeyCode = 0x4E (N), Modifiers = Control
/// - Ctrl+Alt+Delete: VirtualKeyCode = 0x2E (Delete), Modifiers = Control | Alt
/// - F5: VirtualKeyCode = 0x74, Modifiers = None
/// - 2xSHIFT: IsDoubleTap = true, DoubleTapKeyCode = 0xA0 (VK_LSHIFT)
/// </para>
/// <para>
/// This struct implements value equality, meaning two instances with the same
/// virtual key code and modifiers are considered equal, regardless of reference.
/// </para>
/// </remarks>
public readonly struct KeyCombination : IEquatable<KeyCombination>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyCombination"/> struct.
    /// </summary>
    /// <param name="virtualKeyCode">
    /// The Windows virtual key code for the main key.
    /// See: https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
    /// </param>
    /// <param name="modifiers">The modifier keys (Ctrl, Alt, Shift, Win) combined with bitwise OR.</param>
    public KeyCombination(int virtualKeyCode, ModifierKeys modifiers)
    {
        VirtualKeyCode = virtualKeyCode;
        Modifiers = modifiers;
        IsDoubleTap = false;
        DoubleTapKeyCode = 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyCombination"/> struct for a double-tap trigger.
    /// </summary>
    /// <param name="virtualKeyCode">
    /// The Windows virtual key code for the main key (or 0 if only double-tap without follow-up key).
    /// </param>
    /// <param name="modifiers">The modifier keys (Ctrl, Alt, Shift, Win) combined with bitwise OR.</param>
    /// <param name="isDoubleTap">True if this is a double-tap trigger.</param>
    /// <param name="doubleTapKeyCode">The virtual key code of the key that should be double-tapped.</param>
    public KeyCombination(int virtualKeyCode, ModifierKeys modifiers, bool isDoubleTap, int doubleTapKeyCode)
    {
        VirtualKeyCode = virtualKeyCode;
        Modifiers = modifiers;
        IsDoubleTap = isDoubleTap;
        DoubleTapKeyCode = doubleTapKeyCode;
    }

    /// <summary>
    /// Gets the Windows virtual key code for the main key in this combination.
    /// </summary>
    /// <remarks>
    /// Common virtual key codes:
    /// - Letters A-Z: 0x41-0x5A
    /// - Numbers 0-9: 0x30-0x39
    /// - Function keys F1-F12: 0x70-0x7B
    /// - Special keys: Enter (0x0D), Escape (0x1B), Space (0x20), etc.
    /// </remarks>
    public int VirtualKeyCode { get; }

    /// <summary>
    /// Gets the modifier keys (Ctrl, Alt, Shift, Win) for this combination.
    /// </summary>
    public ModifierKeys Modifiers { get; }

    /// <summary>
    /// Gets a value indicating whether this is a double-tap trigger (e.g., 2xSHIFT).
    /// </summary>
    public bool IsDoubleTap { get; }

    /// <summary>
    /// Gets the virtual key code of the key that should be double-tapped.
    /// Only valid when IsDoubleTap is true.
    /// </summary>
    /// <remarks>
    /// This will be set to specific left/right modifier key codes:
    /// - 0xA0: VK_LSHIFT
    /// - 0xA1: VK_RSHIFT
    /// - 0xA2: VK_LCONTROL
    /// - 0xA3: VK_RCONTROL
    /// - 0xA4: VK_LALT
    /// - 0xA5: VK_RALT
    /// - 0x5B: VK_LWIN
    /// - 0x5C: VK_RWIN
    /// </remarks>
    public int DoubleTapKeyCode { get; }

    /// <summary>
    /// Determines whether this instance equals another <see cref="KeyCombination"/> instance.
    /// </summary>
    /// <param name="other">The other instance to compare with.</param>
    /// <returns>True if both the virtual key code and modifiers are equal; otherwise, false.</returns>
    public bool Equals(KeyCombination other)
    {
        return VirtualKeyCode == other.VirtualKeyCode
            && Modifiers == other.Modifiers
            && IsDoubleTap == other.IsDoubleTap
            && DoubleTapKeyCode == other.DoubleTapKeyCode;
    }

    /// <summary>
    /// Determines whether this instance equals the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns>True if obj is a <see cref="KeyCombination"/> and equals this instance; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return obj is KeyCombination other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code for this instance, suitable for use in hash-based collections.
    /// </summary>
    /// <returns>A hash code combining the virtual key code and modifiers.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(VirtualKeyCode, Modifiers, IsDoubleTap, DoubleTapKeyCode);
    }

    /// <summary>
    /// Determines whether two <see cref="KeyCombination"/> instances are equal.
    /// </summary>
    public static bool operator ==(KeyCombination left, KeyCombination right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="KeyCombination"/> instances are not equal.
    /// </summary>
    public static bool operator !=(KeyCombination left, KeyCombination right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Returns a human-readable string representation of this key combination.
    /// </summary>
    /// <returns>A string in the format "CTRL+ALT+VK4E" showing modifiers and virtual key code.</returns>
    /// <example>
    /// Examples:
    /// - "CTRL+VK4E" for Ctrl+N
    /// - "CTRL+ALT+SHIFT+VK2E" for Ctrl+Alt+Shift+Delete
    /// - "VK74" for F5 (no modifiers)
    /// - "2xVKA0" for double-tap left SHIFT
    /// - "2xVKA0+VK31" for double-tap left SHIFT then press 1
    /// </example>
    public override string ToString()
    {
        var parts = new List<string>();

        if (IsDoubleTap)
        {
            parts.Add($"2xVK{DoubleTapKeyCode:X2}");

            if (VirtualKeyCode != 0)
                parts.Add($"VK{VirtualKeyCode:X2}");
        }
        else
        {
            if (Modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("CTRL");
            if (Modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("ALT");
            if (Modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("SHIFT");
            if (Modifiers.HasFlag(ModifierKeys.Win))
                parts.Add("WIN");

            parts.Add($"VK{VirtualKeyCode:X2}");
        }

        return string.Join("+", parts);
    }
}
