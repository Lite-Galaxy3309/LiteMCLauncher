namespace LiteMCLauncher.Models;

/// <summary>
/// A Modrinth category filter option.
/// </summary>
public sealed record ModCategoryOption(string Display, string? Slug)
{
    public override string ToString() => Display;
}
