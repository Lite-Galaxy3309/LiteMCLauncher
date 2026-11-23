namespace LiteMCLauncher.Models;

/// <summary>
/// Progress for a complete modpack installation.
/// </summary>
public sealed record ModpackInstallProgress(double Percentage, string Status);
