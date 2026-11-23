namespace LiteMCLauncher.Models;

/// <summary>
/// A section of the Mod version list, grouped by loader and game version.
/// </summary>
public sealed class ModVersionGroup
{
    public ModVersionGroup(string loader, string gameVersion, IReadOnlyList<ModVersionInfo> items)
    {
        Loader = loader;
        GameVersion = gameVersion;
        Items = items;
    }

    public string Loader { get; }

    public string GameVersion { get; }

    public string Title => $"{Loader} · {GameVersion}";

    public string CountText => $"{Items.Count} 个版本";

    public IReadOnlyList<ModVersionInfo> Items { get; }
}
