namespace LiteMCLauncher.Services;

/// <summary>
/// Shared service instances. Pages are created by the Frame with a
/// parameterless constructor, so a small service locator keeps the wiring
/// explicit without introducing a full DI container.
/// </summary>
public static class AppServices
{
    public static MicrosoftAuthService Auth { get; } = new();

    public static MinecraftInstanceService Instances { get; } = new();

    public static InstanceContentService InstanceContent { get; } = new();

    public static JavaService Java { get; } = new();

    public static PlayerAvatarService Avatars { get; } = new();

    public static MusicService Music { get; } = new();

    public static NewsService News { get; } = new();

    public static LauncherSettingsService Settings { get; } = new();

    public static LauncherState LauncherState { get; } = new();

    public static LaunchPreflightService Preflight { get; } = new(Java, Auth, Settings);

    public static MinecraftInstallService Installer { get; } = new();

    public static ModLoaderInstallService Loaders { get; } = new();

    public static ModPlatformService Mods { get; } = new();

    public static ModpackInstallService Modpacks { get; } = new();

    public static GameLaunchService Launcher { get; } = new();
}
