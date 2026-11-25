using LiteMCLauncher.Services;

namespace LiteMCLauncher.Models;

/// <summary>
/// A Minecraft version available from the official Mojang manifest.
/// </summary>
public sealed record MinecraftVersionInfo(
    string Id,
    string? Type,
    DateTimeOffset ReleaseTime,
    bool IsInstalled)
{
    public string TypeDisplay => LauncherLanguageService.IsEnglish
        ? Type switch
        {
            "release" => "Release",
            "snapshot" => "Snapshot",
            "old_beta" => "Legacy Beta",
            "old_alpha" => "Legacy Alpha",
            _ => "Unknown type",
        }
        : Type switch
        {
            "release" => "正式版",
            "snapshot" => "快照",
            "old_beta" => "旧版 Beta",
            "old_alpha" => "旧版 Alpha",
            _ => "未知类型",
        };

    public string ReleaseDateDisplay => ReleaseTime == default
        ? (LauncherLanguageService.IsEnglish ? "Unknown release date" : "未知发布日期")
        : ReleaseTime.ToLocalTime().ToString("yyyy-MM-dd");

    public string InstallButtonText => IsInstalled
        ? (LauncherLanguageService.IsEnglish ? "Reinstall" : "重新安装")
        : (LauncherLanguageService.IsEnglish ? "Install" : "安装");

    public string IconPath { get; set; } = "ms-appx:///Assets/Loaders/vanilla.png";

    public override string ToString() => $"{Id} ({TypeDisplay})";
}
