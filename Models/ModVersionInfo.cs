namespace LiteMCLauncher.Models;

/// <summary>
/// One downloadable version/file of a Modrinth or CurseForge project.
/// </summary>
public sealed record ModVersionInfo(
    string Id,
    string Name,
    string VersionNumber,
    DateTimeOffset PublishedAt,
    IReadOnlyList<string> GameVersions,
    IReadOnlyList<string> Loaders,
    string? FileName,
    string? DownloadUrl,
    long FileSize)
{
    public string GameVersionsDisplay => GameVersions.Count == 0
        ? "未标注游戏版本"
        : string.Join(", ", GameVersions.Take(6)) + (GameVersions.Count > 6 ? "…" : string.Empty);

    public string LoadersDisplay => Loaders.Count == 0
        ? "通用"
        : string.Join(", ", Loaders.Select(NormalizeLoader));

    private static string NormalizeLoader(string loader) => loader.ToLowerInvariant() switch
    {
        "fabric" => "Fabric",
        "neoforge" => "NeoForge",
        "forge" => "Forge",
        "quilt" => "Quilt",
        _ => loader,
    };

    public string PublishedDisplay => PublishedAt == default
        ? "未知日期"
        : PublishedAt.ToLocalTime().ToString("yyyy-MM-dd");

    public string FileSizeDisplay => FileSize switch
    {
        >= 1024L * 1024 => $"{FileSize / 1024d / 1024d:F1} MB",
        >= 1024 => $"{FileSize / 1024d:F1} KB",
        > 0 => $"{FileSize} B",
        _ => "未知大小",
    };

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? VersionNumber : Name;
}
