namespace ShiftyGrid.Keyboard.Models;

/// <summary>
/// Defines a keyboard shortcut with its associated action and behavior.
/// </summary>
/// <remarks>
/// <para>
/// A ShortcutDefinition represents a complete shortcut specification including:
/// - The key combination that triggers it (e.g., Ctrl+Alt+N)
/// - The action to execute when triggered (identified by ActionId)
/// - Whether it's global or application-specific
/// - Whether to block the key from reaching other applications
/// - Optional association with a mode (for multi-level shortcuts)
/// </para>
/// <para>
/// This class is immutable after construction, making it thread-safe for
/// registration with the shortcut engine.
/// </para>
/// </remarks>
public class ShortcutDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShortcutDefinition"/> class.
    /// </summary>
    /// <param name="id">Unique identifier for this shortcut (e.g., "launch_notepad").</param>
    /// <param name="keyCombination">The key combination that triggers this shortcut.</param>
    /// <param name="actionId">
    /// Identifier for the action to execute (e.g., "launch_notepad", "enter_mode:browser").
    /// </param>
    /// <param name="scope">Whether this is a global shortcut or application-specific.</param>
    /// <param name="blockKey">
    /// If true, prevents the key combination from reaching other applications.
    /// Use with caution as it can override system shortcuts.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="id"/> or <paramref name="actionId"/> is null.
    /// </exception>
    public ShortcutDefinition(string id, KeyCombination keyCombination, string actionId, ShortcutScope scope, bool blockKey, bool exitMode = false)
    {
        // todo: consider - if id not passed / defined can use Guid.ToString?

        Id = id ?? throw new ArgumentNullException(nameof(id));
        KeyCombination = keyCombination;
        ActionId = actionId ?? throw new ArgumentNullException(nameof(actionId));
        Scope = scope;
        BlockKey = blockKey;
        ExitMode = exitMode;
    }

    /// <summary>
    /// Gets the unique identifier for this shortcut.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the key combination that triggers this shortcut.
    /// </summary>
    public KeyCombination KeyCombination { get; }

    /// <summary>
    /// Gets the action identifier that will be executed when this shortcut is triggered.
    /// </summary>
    /// <remarks>
    /// Common action ID patterns:
    /// - Simple actions: "launch_notepad", "save_document"
    /// - Mode entry: "enter_mode:browser", "enter_mode:editing"
    /// </remarks>
    public string ActionId { get; }

    /// <summary>
    /// Gets the scope of this shortcut (global or per-application).
    /// </summary>
    public ShortcutScope Scope { get; }

    /// <summary>
    /// Gets a value indicating whether this shortcut should block the key combination
    /// from being processed by other applications.
    /// </summary>
    /// <remarks>
    /// When true, the keyboard hook returns -1 to prevent the key from propagating.
    /// This effectively "consumes" the keypress. Use carefully as it can override
    /// important system shortcuts.
    /// </remarks>
    public bool BlockKey { get; }

    /// <summary>
    /// Gets or initializes the mode ID if this shortcut is part of a mode.
    /// Null for top-level shortcuts.
    /// </summary>
    /// <remarks>
    /// Mode shortcuts are only active when their associated mode is entered.
    /// For example, pressing "1" might only trigger an action when in "browser" mode.
    /// </remarks>
    public string? ModeId { get; init; }

    /// <summary>
    /// Gets or initializes a value indicating whether this shortcut should automatically
    /// exit the mode after executing its action.
    /// </summary>
    /// <remarks>
    /// When true and this shortcut is part of a mode, the mode will be exited immediately
    /// after the shortcut's action is dispatched. This allows for a streamlined workflow
    /// where executing an action also completes the mode interaction.
    /// Only applies to mode shortcuts. Has no effect on top-level shortcuts.
    /// Default is false.
    /// </remarks>
    public bool ExitMode { get; init; }
}
