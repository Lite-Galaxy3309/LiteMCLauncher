namespace LiteMCLauncher.Models;

/// <summary>
/// Progress information reported while downloading and extracting a JDK.
/// </summary>
public sealed record JavaDownloadProgress(double Percentage, string Status);
