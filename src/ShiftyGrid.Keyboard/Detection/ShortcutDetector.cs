using ShiftyGrid.Keyboard.Models;

namespace ShiftyGrid.Keyboard.Detection;

public class ShortcutDetector
{
    private readonly Dictionary<KeyCombination, List<ShortcutDefinition>> _shortcuts = new();
    private readonly object _lock = new();

    public void RegisterShortcut(ShortcutDefinition shortcut)
    {
        lock (_lock)
        {
            if (!_shortcuts.ContainsKey(shortcut.KeyCombination))
            {
                _shortcuts[shortcut.KeyCombination] = new List<ShortcutDefinition>();
            }

            _shortcuts[shortcut.KeyCombination].Add(shortcut);
        }
    }

    public void UnregisterShortcut(string shortcutId)
    {
        lock (_lock)
        {
            foreach (var kvp in _shortcuts)
            {
                kvp.Value.RemoveAll(s => s.Id == shortcutId);
            }
        }
    }

    public List<ShortcutDefinition> FindMatches(KeyCombination keyCombination, string? activeModeId = null)
    {
        lock (_lock)
        {
            if (!_shortcuts.TryGetValue(keyCombination, out var matches))
            {
                return new List<ShortcutDefinition>();
            }

            // If in a mode, return shortcuts for that mode AND mode activation shortcuts
            if (activeModeId != null)
            {
                return matches.Where(s =>
                    s.ModeId == activeModeId ||
                    (s.ModeId == null && s.ActionId.StartsWith("enter_mode:"))).ToList();
            }

            // Otherwise return shortcuts not bound to any mode (including mode activation shortcuts)
            return matches.Where(s => s.ModeId == null).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _shortcuts.Clear();
        }
    }
}
