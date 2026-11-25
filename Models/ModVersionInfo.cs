using LiteMCLauncher.Services;

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
        ? (LauncherLanguageService.IsEnglish ? "No game version specified" : "未标注游戏版本")
        : string.Join(", ", GameVersions.Take(6)) + (GameVersions.Count > 6 ? "…" : string.Empty);

    public string LoadersDisplay => Loaders.Count == 0
        ? (LauncherLanguageService.IsEnglish ? "Universal" : "通用")
        : string.Join(", ", Loaders.Select(NormalizeLoader));

    public string DownloadButtonText => LauncherLanguageService.IsEnglish ? "Download and install" : "下载并安装";

    private static string NormalizeLoader(string loader) => loader.ToLowerInvariant() switch
    {
        "fabric" => "Fabric",
        "neoforge" => "NeoForge",
        "forge" => "Forge",
        "quilt" => "Quilt",
        _ => loader,
    };

    public string PublishedDisplay => PublishedAt == default
        ? (LauncherLanguageService.IsEnglish ? "Unknown date" : "未知日期")
        : PublishedAt.ToLocalTime().ToString("yyyy-MM-dd");

    public string FileSizeDisplay => FileSize switch
    {
        >= 1024L * 1024 => $"{FileSize / 1024d / 1024d:F1} MB",
        >= 1024 => $"{FileSize / 1024d:F1} KB",
        > 0 => $"{FileSize} B",
        _ => LauncherLanguageService.IsEnglish ? "Unknown size" : "未知大小",
    };

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? VersionNumber : Name;
}
