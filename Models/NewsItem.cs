using LiteMCLauncher.Services;

namespace LiteMCLauncher.Models;

public sealed class NewsItem
{
    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public string Link { get; set; } = string.Empty;

    public string ContentPath { get; set; } = string.Empty;

    public string HtmlBody { get; set; } = string.Empty;

    public DateTimeOffset Date { get; set; }

    public string DateDisplay => Date == default
        ? string.Empty
        : Date.ToLocalTime().ToString("yyyy-MM-dd");

    public string CategoryDisplay => LauncherLanguageService.IsEnglish
        ? Category switch
        {
            "快照更新" => "Snapshot update",
            "Java 版更新" => "Java Edition update",
            _ => LauncherLanguageService.Translate(Category),
        }
        : Category;

    public string SourceDisplay => LauncherLanguageService.IsEnglish
        ? Source switch
        {
            "Mojang 新闻" => "Mojang news",
            "Mojang 更新" => "Mojang update",
            _ => LauncherLanguageService.Translate(Source),
        }
        : Source;

    public Uri? LinkUri => Uri.TryCreate(Link, UriKind.Absolute, out var uri) ? uri : null;

    public bool HasLink => LinkUri is not null;

    public override string ToString() => Title;
}
