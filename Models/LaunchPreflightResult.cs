namespace LiteMCLauncher.Models;

/// <summary>
/// Result of the launch preflight: account state plus the Java runtime that
/// will actually be used for this instance.
/// </summary>
public sealed record LaunchPreflightResult(
    bool LoginReady,
    bool JavaReady,
    bool JavaVersionMatches,
    string? JavaPath,
    string? JavaVersion,
    int JavaMajor,
    int RequiredJavaMajor,
    string? JavaDisplayName,
    string? ErrorMessage)
{
    public bool CanLaunch => LoginReady && JavaReady && JavaVersionMatches;

    public static LaunchPreflightResult LoginFailed(string message) =>
        new(false, false, false, null, null, 0, 0, null, message);

    public static LaunchPreflightResult JavaMissing(int required, string? message = null) =>
        new(true, false, false, null, null, 0, required, null,
            message ?? $"实例需要 Java {required}，但没有找到可用的 Java 运行时。");

    public static LaunchPreflightResult JavaMismatch(
        string? path,
        string? version,
        int major,
        int required,
        string? displayName) =>
        new(true, true, false, path, version, major, required, displayName,
            $"实例需要 Java {required}，当前使用的是 Java {major}。");
}
