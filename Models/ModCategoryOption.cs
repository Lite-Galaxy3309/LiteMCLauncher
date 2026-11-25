using LiteMCLauncher.Services;

namespace LiteMCLauncher.Models;

/// <summary>
/// A Modrinth category filter option.
/// </summary>
public sealed record ModCategoryOption(string Name, string? Slug)
{
    public string Display => Slug is null
        ? (LauncherLanguageService.IsEnglish ? "All categories" : "全部分类")
        : Name;

    public override string ToString() => Display;
}
