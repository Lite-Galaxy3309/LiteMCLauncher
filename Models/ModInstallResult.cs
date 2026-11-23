namespace LiteMCLauncher.Models;

/// <summary>
/// Result of downloading a Mod or modpack file.
/// </summary>
public sealed record ModInstallResult(
    string FilePath,
    string FileName,
    string VersionName,
    bool IsModpack);
