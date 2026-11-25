using LiteMCLauncher.Services;

namespace LiteMCLauncher.Models;

/// <summary>
/// A project returned by Modrinth or CurseForge search.
/// </summary>
public sealed record ModSearchResult(
    string Source,
    string ProjectId,
    string? Slug,
    string Title,
    string Author,
    string Description,
    string ProjectType,
    long Downloads,
    IReadOnlyList<string> Categories,
    string? IconUrl = null,
    string? ProjectUrl = null)
{
    public bool IsModpack => string.Equals(ProjectType, "modpack", StringComparison.OrdinalIgnoreCase);

    public string ProjectTypeDisplay => IsModpack
        ? (LauncherLanguageService.IsEnglish ? "Modpack" : "整合包")
        : "Mod";

    public string DownloadsDisplay => Downloads switch
    {
        >= 1_000_000_000 => $"{Downloads / 1_000_000_000d:0.#}B",
        >= 1_000_000 => $"{Downloads / 1_000_000d:0.#}M",
        >= 1_000 => $"{Downloads / 1_000d:0.#}K",
        _ => Downloads.ToString(),
    };

    public string ActionText => IsModpack
        ? (LauncherLanguageService.IsEnglish ? "Install modpack" : "一键安装")
        : (LauncherLanguageService.IsEnglish ? "Install" : "安装");

    public override string ToString() => $"{Title} · {ProjectTypeDisplay} · {Source}";
}
