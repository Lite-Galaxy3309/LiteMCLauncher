using CmlLib.Core;
using Windows.Storage;

namespace LiteMCLauncher.Services;

/// <summary>
/// Central paths used by the launcher. Minecraft data can be overridden with
/// the <c>LITMC_MINECRAFT_DIR</c> environment variable.
/// </summary>
public static class AppPaths
{
    public static string MinecraftDirectory { get; } = ResolveMinecraftDirectory();

    public static string VersionsDirectory => Path.Combine(MinecraftDirectory, "versions");

    public static string DataDirectory { get; } = ResolveDataDirectory();

    public static string JavaDirectory => Path.Combine(MinecraftDirectory, "java");

    public static string AccountsFile => Path.Combine(DataDirectory, "accounts.json");

    /// <summary>
    /// Multi-account list requested by the launcher UI. This intentionally
    /// lives in the game directory so it travels with the Minecraft install.
    /// </summary>
    public static string LauncherAccountsFile => Path.Combine(MinecraftDirectory, "launcher_accounts.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(JavaDirectory);
    }

    private static string ResolveMinecraftDirectory()
    {
        var custom = Environment.GetEnvironmentVariable("LITMC_MINECRAFT_DIR");
        return string.IsNullOrWhiteSpace(custom)
            ? MinecraftPath.GetOSDefaultPath()
            : Path.GetFullPath(custom);
    }

    private static string ResolveDataDirectory()
    {
        // A packaged WinUI app cannot freely write to %LOCALAPPDATA%; use the
        // package's LocalState folder, which is also valid for Store/MSIX.
        try
        {
            var localFolder = ApplicationData.Current.LocalFolder.Path;
            if (!string.IsNullOrWhiteSpace(localFolder))
            {
                return localFolder;
            }
        }
        catch (Exception)
        {
            // No package identity (unpackaged run).
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LiteMCLauncher");
    }
}
