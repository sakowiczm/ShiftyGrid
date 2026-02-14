namespace ShiftyGrid.Keyboard.Models;

/// <summary>
/// Defines the scope of a keyboard shortcut - whether it works globally or only in specific applications.
/// </summary>
public enum ShortcutScope
{
    /// <summary>
    /// Global shortcuts work anywhere in the system, regardless of which application has focus.
    /// Use for system-wide productivity shortcuts.
    /// </summary>
    Global,

    /// <summary>
    /// Per-application shortcuts only work when your application has focus.
    /// Use for application-specific functionality.
    /// </summary>
    /// <remarks>
    /// Note: This is currently not fully implemented and treated the same as Global.
    /// Full per-application support would require checking the active window handle.
    /// </remarks>
    PerApplication
}
