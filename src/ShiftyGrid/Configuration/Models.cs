using ShiftyGrid.Infrastructure.Models;
using YamlDotNet.Serialization;

namespace ShiftyGrid.Configuration;

/// <summary>
/// Root configuration for ShiftyGrid
/// </summary>
public class ShiftyGridConfig
{
    [YamlMember(Alias = "general")]
    public GeneralSettings General { get; set; } = new();

    [YamlMember(Alias = "keyboard")]
    public KeyboardSettings Keyboard { get; set; } = new();

    [YamlMember(Alias = "organize")]
    public OrganizeSettings Organize { get; set; } = new();

    [YamlMember(Alias = "ignore")]
    public IgnoreSettings Ignore { get; set; } = new();
}

/// <summary>
/// General application settings
/// </summary>
public class GeneralSettings
{
    [YamlMember(Alias = "gap")]
    public int Gap { get; set; } = 4;

    [YamlMember(Alias = "proximity_threshold")]
    public int ProximityThreshold { get; set; } = 20;

    [YamlMember(Alias = "auto_organize")]
    public bool AutoOrganize { get; set; } = true;

    [YamlMember(Alias = "log_level")]
    public string LogLevel { get; set; } = "info";
}

/// <summary>
/// Keyboard shortcut configuration
/// </summary>
public class KeyboardSettings
{
    [YamlMember(Alias = "shortcuts")]
    public List<ShortcutConfig> Shortcuts { get; set; } = new();

    [YamlMember(Alias = "modes")]
    public List<ModeConfig> Modes { get; set; } = new();
}

/// <summary>
/// Single shortcut configuration
/// </summary>
public class ShortcutConfig
{
    [YamlMember(Alias = "bindings")]
    public List<string> Bindings { get; set; } = new();

    [YamlMember(Alias = "command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Whether to block the key from reaching other applications. Default: true
    /// Only include in YAML if false
    /// </summary>
    [YamlMember(Alias = "block_key")]
    public bool BlockKey { get; set; } = true;
}

/// <summary>
/// Modal shortcut mode configuration
/// </summary>
public class ModeConfig
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "activation")]
    public List<string> Activation { get; set; } = new();

    [YamlMember(Alias = "allow_escape")]
    public bool AllowEscape { get; set; } = true;

    /// <summary>
    /// Timeout in milliseconds. Default: 5000
    /// Only include in YAML if different from default
    /// </summary>
    [YamlMember(Alias = "timeout_ms")]
    public int TimeoutMs { get; set; } = 5000;

    [YamlMember(Alias = "shortcuts")]
    public List<ModeShortcutConfig> Shortcuts { get; set; } = new();
}

/// <summary>
/// Shortcut within a modal mode
/// </summary>
public class ModeShortcutConfig
{
    [YamlMember(Alias = "bindings")]
    public List<string> Bindings { get; set; } = new();

    [YamlMember(Alias = "command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Whether to exit mode after executing this shortcut. Default: true
    /// Only include in YAML if false (staying in mode)
    /// </summary>
    [YamlMember(Alias = "exit_mode")]
    public bool ExitMode { get; set; } = true;

    /// <summary>
    /// Whether to block the key from reaching other applications. Default: true
    /// Only include in YAML if false
    /// </summary>
    [YamlMember(Alias = "block_key")]
    public bool BlockKey { get; set; } = true;
}

/// <summary>
/// Window organization rules
/// </summary>
public class OrganizeSettings
{
    [YamlMember(Alias = "rules")]
    public List<OrganizeRule> Rules { get; set; } = new();
}

/// <summary>
/// Single organize rule: match + command
/// </summary>
public class OrganizeRule
{
    [YamlMember(Alias = "match")]
    public WindowMatchConfig Match { get; set; } = new();

    [YamlMember(Alias = "command")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Parsed coordinates data (populated during configuration load)
    /// This is NOT serialized to/from YAML - it's derived from Command
    /// </summary>
    [YamlIgnore]
    public Coordinates? ParsedCoordinates { get; set; }
}

/// <summary>
/// Window ignore rules (global filtering)
/// </summary>
public class IgnoreSettings
{
    [YamlMember(Alias = "rules")]
    public List<IgnoreRule> Rules { get; set; } = new();
}

/// <summary>
/// Single ignore rule: match only (no command)
/// </summary>
public class IgnoreRule
{
    [YamlMember(Alias = "match")]
    public WindowMatchConfig Match { get; set; } = new();
}

/// <summary>
/// Window matching configuration
/// Supports exact match (default) or regex with "regex:" prefix
/// </summary>
public class WindowMatchConfig
{
    [YamlMember(Alias = "title_pattern")]
    public string? TitlePattern { get; set; }

    [YamlMember(Alias = "class_name")]
    public string? ClassName { get; set; }

    [YamlMember(Alias = "process_name")]
    public string? ProcessName { get; set; }
}
