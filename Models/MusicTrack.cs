using Microsoft.UI.Xaml.Media;

namespace LiteMCLauncher.Models;

public sealed class MusicTrack
{
    public required string FilePath { get; init; }

    public string Title { get; init; } = "未知歌曲";

    public string Artist { get; init; } = "未知艺术家";

    public string Album { get; init; } = "未知专辑";

    public TimeSpan Duration { get; init; }

    public ImageSource? AlbumArt { get; set; }

    public string DurationDisplay => Duration > TimeSpan.Zero
        ? $"{(int)Duration.TotalMinutes}:{Duration.Seconds:D2}"
        : "--:--";

    public override string ToString() => $"{Title} - {Artist}";
}

public enum MusicRepeatMode
{
    List,
    Single,
    Shuffle,
}
