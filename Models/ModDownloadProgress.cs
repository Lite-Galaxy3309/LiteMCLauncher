namespace LiteMCLauncher.Models;

/// <summary>
/// Progress reported while downloading a Mod or modpack file.
/// </summary>
public sealed record ModDownloadProgress(double Percentage, string Status);
