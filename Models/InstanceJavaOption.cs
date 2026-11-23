namespace LiteMCLauncher.Models;

/// <summary>
/// A Java choice for one instance: either the launcher default or a concrete
/// java.exe/javaw.exe path.
/// </summary>
public sealed record InstanceJavaOption(string Display, string? Path, bool IsDefault = false)
{
    public override string ToString() => Display;
}
