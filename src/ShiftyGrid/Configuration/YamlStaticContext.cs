using YamlDotNet.Serialization;

namespace ShiftyGrid.Configuration;

/// <summary>
/// Static context for AOT-compatible YAML serialization/deserialization
/// </summary>
[YamlStaticContext]
[YamlSerializable(typeof(ShiftyGridConfig))]
[YamlSerializable(typeof(GeneralSettings))]
[YamlSerializable(typeof(KeyboardSettings))]
[YamlSerializable(typeof(ShortcutConfig))]
[YamlSerializable(typeof(ModeConfig))]
[YamlSerializable(typeof(ModeShortcutConfig))]
[YamlSerializable(typeof(OrganizeSettings))]
[YamlSerializable(typeof(OrganizeRule))]
[YamlSerializable(typeof(IgnoreSettings))]
[YamlSerializable(typeof(IgnoreRule))]
[YamlSerializable(typeof(WindowMatchConfig))]
public partial class YamlStaticContext
{
}
