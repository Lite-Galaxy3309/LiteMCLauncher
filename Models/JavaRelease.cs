namespace LiteMCLauncher.Models;

/// <summary>
/// A downloadable Adoptium Temurin JDK package.
/// </summary>
public sealed record JavaRelease(
    int FeatureVersion,
    string ReleaseName,
    string SemanticVersion,
    string PackageName,
    string DownloadUrl,
    string? Sha256,
    long Size);
