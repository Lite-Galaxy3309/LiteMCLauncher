namespace LiteMCLauncher.Models;

/// <summary>
/// Per-instance overrides for the game launch settings. When <see cref="Enabled"/>
/// is false the instance follows the launcher-wide settings.
/// </summary>
public sealed class InstanceGameSettings
{
    public bool Enabled { get; set; }

    public string? JavaPath { get; set; }

    public int MaximumRamMb { get; set; } = 4096;

    public string? JvmArguments { get; set; }

    public string GameLanguage { get; set; } = "zh_cn";

    public InstanceGameSettings Clone() => new()
    {
        Enabled = Enabled,
        JavaPath = JavaPath,
        MaximumRamMb = MaximumRamMb,
        JvmArguments = JvmArguments,
        GameLanguage = GameLanguage,
    };
}
