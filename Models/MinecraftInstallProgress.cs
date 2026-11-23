namespace LiteMCLauncher.Models;

/// <summary>
/// Progress reported while CmlLib downloads a Minecraft version and its assets.
/// </summary>
public sealed record MinecraftInstallProgress(
    double Percentage,
    string Status,
    string? CurrentFile);
