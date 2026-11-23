namespace LiteMCLauncher.Models;

/// <summary>
/// An installed Java runtime managed by the launcher.
/// </summary>
public sealed record JavaRuntime(string Name, string Version, string HomePath)
{
    public string JavaExecutable => Path.Combine(HomePath, "bin", "java.exe");

    public string JavaWExecutable => Path.Combine(HomePath, "bin", "javaw.exe");

    public string PreferredExecutable => File.Exists(JavaWExecutable) ? JavaWExecutable : JavaExecutable;
}
