namespace LiteMCLauncher.Models;

/// <summary>
/// A world, shader pack, resource pack or mod inside an instance directory.
/// </summary>
public sealed record InstanceContentItem(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Size,
    DateTimeOffset ModifiedAt)
{
    public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE8A5";

    public string DetailText => IsDirectory
        ? "文件夹"
        : Size switch
        {
            >= 1024L * 1024 => $"{Size / 1024d / 1024d:F1} MB",
            >= 1024 => $"{Size / 1024d:F1} KB",
            _ => $"{Size} B",
        };

    public string ModifiedDisplay => ModifiedAt == default
        ? "未知时间"
        : ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public override string ToString() => Name;
}
