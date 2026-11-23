namespace LiteMCLauncher.Models;

/// <summary>
/// A local Minecraft instance (one folder under <c>.minecraft/versions</c>).
/// </summary>
public sealed record MinecraftInstance(
    string Id,
    string DirectoryPath,
    string GameVersion,
    string? VersionType,
    string? Loader,
    DateTimeOffset LastWriteTime,
    string? CustomName = null)
{
    public string DisplayName => string.IsNullOrWhiteSpace(CustomName) ? Id : CustomName;

    public bool HasCustomName => !string.IsNullOrWhiteSpace(CustomName);

    public string IconPath => LoaderDisplay switch
    {
        "Fabric" => "ms-appx:///Assets/Loaders/fabric.png",
        "Forge" => "ms-appx:///Assets/Loaders/forge.png",
        "NeoForge" => "ms-appx:///Assets/Loaders/neoforge.png",
        "Quilt" => "ms-appx:///Assets/Loaders/quilt.png",
        _ => "ms-appx:///Assets/Loaders/vanilla.png",
    };

    public string LoaderDisplay => string.IsNullOrWhiteSpace(Loader) ? "原版" : Loader;

    public string VersionTypeDisplay => VersionType switch
    {
        "release" => "正式版",
        "snapshot" => "快照",
        "old_beta" => "旧版 Beta",
        "old_alpha" => "旧版 Alpha",
        _ => "未知类型",
    };

    public string Summary => $"Minecraft {GameVersion} · {LoaderDisplay}";

    public override string ToString() => $"{DisplayName} ({Summary})";

    public string UpdatedDisplay
    {
        get
        {
            var local = LastWriteTime.ToLocalTime();
            var now = DateTimeOffset.Now;
            var days = (now.Date - local.Date).Days;

            if (days <= 0)
            {
                var elapsed = now - local;
                return elapsed.TotalMinutes < 2 ? "刚刚" : $"今天 {local:HH:mm}";
            }

            return days == 1 ? $"昨天 {local:HH:mm}" : days < 7 ? $"{days} 天前" : local.ToString("yyyy-MM-dd");
        }
    }
}
