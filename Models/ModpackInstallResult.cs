namespace LiteMCLauncher.Models;

/// <summary>
/// Result of a one-stop modpack installation.
/// </summary>
public sealed record ModpackInstallResult(
    string InstanceId,
    string ModpackName,
    int FileCount,
    string LoaderDisplay);
