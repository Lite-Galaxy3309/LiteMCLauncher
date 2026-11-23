namespace LiteMCLauncher.Models;

public enum ModLoaderType
{
    Vanilla,
    Fabric,
    Forge,
    NeoForge,
    Quilt,
}

public sealed record ModLoaderInstallResult(
    string VersionId,
    ModLoaderType Loader,
    string? LoaderVersion)
{
    public string LoaderDisplay => Loader switch
    {
        ModLoaderType.Fabric => "Fabric",
        ModLoaderType.Forge => "Forge",
        ModLoaderType.NeoForge => "NeoForge",
        ModLoaderType.Quilt => "Quilt",
        _ => "原版",
    };
}
