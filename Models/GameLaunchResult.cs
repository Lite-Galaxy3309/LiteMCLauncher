namespace LiteMCLauncher.Models;

/// <summary>
/// Result of a successful game process launch.
/// </summary>
public sealed record GameLaunchResult(int ProcessId, string VersionId);
