namespace ShiftyGrid.Keyboard.Models;

/// <summary>
/// Defines a modal shortcut mode that enables multi-level keyboard shortcuts.
/// </summary>
/// <remarks>
/// <para>
/// Modes allow you to create hierarchical shortcut systems. After pressing an activation
/// shortcut, you enter the mode where simpler key combinations trigger specific actions.
/// This reduces the need for complex modifier combinations and logically groups related shortcuts.
/// </para>
/// <para>
/// Example workflow:
/// 1. User presses Ctrl+Alt+B (activation keys) to enter "Browser" mode
/// 2. System displays available shortcuts (1, 2, 3 for different browsers)
/// 3. User presses "1" to launch Chrome
/// 4. Mode exits automatically (or via timeout/ESC)
/// </para>
/// <para>
/// Benefits:
/// - Reduces finger gymnastics with complex modifier combinations
/// - Groups related shortcuts logically (all browser launchers in one mode)
/// - Provides context-aware shortcut hints
/// - Avoids conflicts with system shortcuts
/// </para>
/// </remarks>
public class ModeDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModeDefinition"/> class.
    /// </summary>
    /// <param name="id">Unique identifier for this mode (e.g., "browser", "editor").</param>
    /// <param name="name">Human-readable name for this mode (e.g., "Browser Mode").</param>
    /// <param name="activationKeys">The key combination that enters this mode.</param>
    /// <param name="timeoutMs">
    /// Timeout in milliseconds before automatically exiting the mode.
    /// Use 0 or negative to disable timeout.
    /// </param>
    /// <param name="allowEscape">
    /// If true, pressing ESC will exit the mode immediately.
    /// If false, ESC is treated like any other key.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="id"/> or <paramref name="name"/> is null.
    /// </exception>
    public ModeDefinition(string id, string name, int timeoutMs, bool allowEscape)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Name = name ?? throw new ArgumentNullException(nameof(name));
        TimeoutMs = timeoutMs;
        AllowEscape = allowEscape;
        Shortcuts = new List<ShortcutDefinition>();
    }

    /// <summary>
    /// Gets the unique identifier for this mode.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the human-readable name for this mode, used for display purposes.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the timeout in milliseconds before automatically exiting this mode.
    /// </summary>
    /// <remarks>
    /// The timer resets on each keypress within the mode, so the timeout only
    /// occurs if the user stops interacting. Set to 0 or negative to disable timeout.
    /// Typical values: 3000-10000ms (3-10 seconds).
    /// </remarks>
    public int TimeoutMs { get; }

    /// <summary>
    /// Gets a value indicating whether pressing ESC will immediately exit this mode.
    /// </summary>
    /// <remarks>
    /// Recommended to set true for user-friendly behavior, allowing users to
    /// cancel out of a mode if they enter it accidentally.
    /// </remarks>
    public bool AllowEscape { get; }

    /// <summary>
    /// Gets the list of shortcuts available within this mode.
    /// </summary>
    /// <remarks>
    /// These shortcuts are only active when the mode is entered. They typically
    /// use simpler key combinations (like single letters or numbers) since the
    /// mode provides context.
    /// </remarks>
    public List<ShortcutDefinition> Shortcuts { get; }

    // todo: update

    /// <summary>
    /// Creates an activation shortcut for this mode using the configured activation keys.
    /// </summary>
    /// <param name="id">
    /// Optional custom ID for the activation shortcut. If null, generates "{ModeId}_activate".
    /// </param>
    /// <param name="scope">
    /// The scope for the activation shortcut (default: Global).
    /// </param>
    /// <param name="blockKey">
    /// Whether the activation shortcut should block the key from propagating to other applications (default: true).
    /// </param>
    /// <returns>
    /// A ShortcutDefinition configured to activate this mode when triggered.
    /// </returns>
    /// <remarks>
    /// This factory method provides a strongly-typed way to create mode activation shortcuts,
    /// ensuring the activation shortcut is properly linked to this mode without manual string construction.
    ///
    /// Example usage:
    /// <code>
    /// var mode = new ModeDefinition("move_mode", "Move Mode",
    ///     new KeyCombination(0x53, ModifierKeys.Control | ModifierKeys.Shift), 5000, true);
    /// var activationShortcut = mode.CreateActivationShortcut();
    /// keyboardEngine.RegisterShortcut(activationShortcut);
    /// keyboardEngine.RegisterMode(mode);
    /// </code>
    /// </remarks>
    public ShortcutDefinition CreateActivationShortcut(
        KeyCombination activationKeys,
        ShortcutScope scope = ShortcutScope.Global,
        bool blockKey = true)
    {
        return new ShortcutDefinition(
            id: $"{Id}_activate",
            keyCombination: activationKeys,
            actionId: $"enter_mode:{Id}",
            scope: scope,
            blockKey: blockKey
        );
    }
}
