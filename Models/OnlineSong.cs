using LiteMCLauncher.Services;

namespace LiteMCLauncher.Models;

public sealed class OnlineSong
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Artists { get; set; } = string.Empty;

    public string Album { get; set; } = string.Empty;

    public string CoverUrl { get; set; } = string.Empty;

    public int DurationMs { get; set; }

    public string? PlayUrl { get; set; }

    public string DurationDisplay => DurationMs <= 0
        ? "--:--"
        : TimeSpan.FromMilliseconds(DurationMs).ToString(@"mm\:ss");

    public string Subtitle => string.IsNullOrWhiteSpace(Album)
        ? Artists
        : $"{Artists} · {Album}";

    public override string ToString() => $"{Name} - {Artists}";
}

public sealed class OnlineHistoryItem
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Artists { get; set; } = string.Empty;

    public string CoverUrl { get; set; } = string.Empty;

    public DateTimeOffset PlayedAt { get; set; }

    public string PlayedAtDisplay => PlayedAt == default
        ? string.Empty
        : PlayedAt.ToLocalTime().ToString("MM-dd HH:mm");

    public string Subtitle => Artists;

    public string DeleteButtonText => LauncherLanguageService.IsEnglish ? "Delete" : "删除";
}
