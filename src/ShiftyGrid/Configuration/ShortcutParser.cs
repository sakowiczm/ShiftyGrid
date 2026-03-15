using ShiftyGrid.Keyboard.Helpers;
using ShiftyGrid.Keyboard.Models;

namespace ShiftyGrid.Configuration;

/// <summary>
/// Parses shortcut strings (e.g., "ctrl+alt+left") into KeyCombination objects
/// </summary>
public static class ShortcutParser
{
    private static readonly Dictionary<string, int> KeyNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // Letters
        { "a", Keys.VK_A }, { "b", Keys.VK_B }, { "c", Keys.VK_C }, { "d", Keys.VK_D },
        { "e", Keys.VK_E }, { "f", Keys.VK_F }, { "g", Keys.VK_G }, { "h", Keys.VK_H },
        { "i", Keys.VK_I }, { "j", Keys.VK_J }, { "k", Keys.VK_K }, { "l", Keys.VK_L },
        { "m", Keys.VK_M }, { "n", Keys.VK_N }, { "o", Keys.VK_O }, { "p", Keys.VK_P },
        { "q", Keys.VK_Q }, { "r", Keys.VK_R }, { "s", Keys.VK_S }, { "t", Keys.VK_T },
        { "u", Keys.VK_U }, { "v", Keys.VK_V }, { "w", Keys.VK_W }, { "x", Keys.VK_X },
        { "y", Keys.VK_Y }, { "z", Keys.VK_Z },

        // Numbers
        { "0", Keys.VK_0 }, { "1", Keys.VK_1 }, { "2", Keys.VK_2 }, { "3", Keys.VK_3 },
        { "4", Keys.VK_4 }, { "5", Keys.VK_5 }, { "6", Keys.VK_6 }, { "7", Keys.VK_7 },
        { "8", Keys.VK_8 }, { "9", Keys.VK_9 },

        // Arrow keys
        { "left", Keys.VK_LEFT }, { "right", Keys.VK_RIGHT },
        { "up", Keys.VK_UP }, { "down", Keys.VK_DOWN },

        // Function keys
        { "f1", Keys.VK_F1 }, { "f2", Keys.VK_F2 }, { "f3", Keys.VK_F3 }, { "f4", Keys.VK_F4 },
        { "f5", Keys.VK_F5 }, { "f6", Keys.VK_F6 }, { "f7", Keys.VK_F7 }, { "f8", Keys.VK_F8 },
        { "f9", Keys.VK_F9 }, { "f10", Keys.VK_F10 }, { "f11", Keys.VK_F11 }, { "f12", Keys.VK_F12 },

        // Special keys
        { "space", Keys.VK_SPACE }, { "enter", Keys.VK_RETURN }, { "return", Keys.VK_RETURN },
        { "escape", Keys.VK_ESCAPE }, { "esc", Keys.VK_ESCAPE }, { "tab", Keys.VK_TAB },
        { "backspace", Keys.VK_BACK }, { "back", Keys.VK_BACK }, { "delete", Keys.VK_DELETE },
        { "del", Keys.VK_DELETE }, { "insert", Keys.VK_INSERT }, { "ins", Keys.VK_INSERT },
        { "home", Keys.VK_HOME }, { "end", Keys.VK_END },
        { "pageup", Keys.VK_PRIOR }, { "pagedown", Keys.VK_NEXT },
        { "pgup", Keys.VK_PRIOR }, { "pgdn", Keys.VK_NEXT },

        // Punctuation and symbols (OEM keys)
        { ",", Keys.VK_OEM_COMMA }, { "comma", Keys.VK_OEM_COMMA },
        { ".", Keys.VK_OEM_PERIOD }, { "period", Keys.VK_OEM_PERIOD },
        { "/", Keys.VK_OEM_2 }, { "slash", Keys.VK_OEM_2 },
        { ";", Keys.VK_OEM_1 }, { "semicolon", Keys.VK_OEM_1 },
        { "'", Keys.VK_OEM_7 }, { "quote", Keys.VK_OEM_7 },
        { "[", Keys.VK_OEM_4 }, { "openbracket", Keys.VK_OEM_4 },
        { "]", Keys.VK_OEM_6 }, { "closebracket", Keys.VK_OEM_6 },
        { "\\", Keys.VK_OEM_5 }, { "backslash", Keys.VK_OEM_5 },
        { "-", Keys.VK_OEM_MINUS }, { "minus", Keys.VK_OEM_MINUS },
        { "=", Keys.VK_OEM_PLUS }, { "equals", Keys.VK_OEM_PLUS }, { "plus", Keys.VK_OEM_PLUS },
        { "`", Keys.VK_OEM_3 }, { "backtick", Keys.VK_OEM_3 },

        // Numpad
        { "numpad0", Keys.VK_NUMPAD0 }, { "numpad1", Keys.VK_NUMPAD1 },
        { "numpad2", Keys.VK_NUMPAD2 }, { "numpad3", Keys.VK_NUMPAD3 },
        { "numpad4", Keys.VK_NUMPAD4 }, { "numpad5", Keys.VK_NUMPAD5 },
        { "numpad6", Keys.VK_NUMPAD6 }, { "numpad7", Keys.VK_NUMPAD7 },
        { "numpad8", Keys.VK_NUMPAD8 }, { "numpad9", Keys.VK_NUMPAD9 },
    };

    private static readonly Dictionary<string, ModifierKeys> ModifierMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "ctrl", ModifierKeys.Control }, { "control", ModifierKeys.Control },
        { "alt", ModifierKeys.Alt },
        { "shift", ModifierKeys.Shift },
        { "win", ModifierKeys.Win }, { "windows", ModifierKeys.Win }, { "super", ModifierKeys.Win }
    };

    /// <summary>
    /// Parses a shortcut string like "ctrl+alt+left" into a KeyCombination
    /// </summary>
    public static KeyCombination Parse(string shortcutString)
    {
        if (string.IsNullOrWhiteSpace(shortcutString))
        {
            throw new ConfigurationException("Shortcut string cannot be empty");
        }

        var parts = shortcutString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            throw new ConfigurationException($"Invalid shortcut string: {shortcutString}");
        }

        ModifierKeys modifiers = ModifierKeys.None;
        int? virtualKeyCode = null;

        foreach (var part in parts)
        {
            // Check if it's a modifier
            if (ModifierMap.TryGetValue(part, out var modifier))
            {
                modifiers |= modifier;
            }
            // Check if it's a key
            else if (KeyNameMap.TryGetValue(part, out var keyCode))
            {
                if (virtualKeyCode.HasValue)
                {
                    throw new ConfigurationException($"Multiple keys specified in shortcut: {shortcutString}");
                }
                virtualKeyCode = keyCode;
            }
            else
            {
                throw new ConfigurationException($"Unknown key or modifier in shortcut '{shortcutString}': {part}");
            }
        }

        if (!virtualKeyCode.HasValue)
        {
            throw new ConfigurationException($"No key specified in shortcut: {shortcutString}");
        }

        return new KeyCombination(virtualKeyCode.Value, modifiers);
    }
}
