namespace LiteMCLauncher.Models;

public sealed class LogFileItem
{
    public string Name { get; set; } = string.Empty;

    public string FullPath { get; set; } = string.Empty;

    public DateTimeOffset ModifiedAt { get; set; }

    public string ModifiedDisplay => ModifiedAt == default
        ? string.Empty
        : ModifiedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public override string ToString() => Name;
}
