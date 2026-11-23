using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Checks the account and Java runtime before launching. Resource integrity is
/// enforced by CmlLib's installer immediately before the process is built.
/// </summary>
public sealed class LaunchPreflightService
{
    private readonly JavaService _javaService;
    private readonly MicrosoftAuthService _authService;
    private readonly LauncherSettingsService _settingsService;

    public LaunchPreflightService(
        JavaService javaService,
        MicrosoftAuthService authService,
        LauncherSettingsService settingsService)
    {
        _javaService = javaService;
        _authService = authService;
        _settingsService = settingsService;
    }

    public async Task<LaunchPreflightResult> CheckAsync(
        MinecraftInstance instance,
        AccountModel? account,
        CancellationToken cancellationToken = default)
    {
        if (account is null)
        {
            return LaunchPreflightResult.LoginFailed("请先登录 Microsoft 账号或添加离线账号。");
        }

        if (!account.IsOffline)
        {
            var ready = await _authService.EnsureAccountReadyAsync(account.Id, cancellationToken);
            if (!ready)
            {
                return LaunchPreflightResult.LoginFailed(
                    "Microsoft 登录已失效且无法自动刷新，请重新登录该账号。");
            }

            account = _authService.CurrentAccount ?? account;
        }

        var requiredMajor = JavaService.GetRequiredJavaMajor(instance.GameVersion);
        var instanceSettings = InstanceSettingsStore.Get(instance.Id);
        var useInstanceSettings = instanceSettings.Enabled;
        var configuredJava = useInstanceSettings
            ? instanceSettings.JavaPath
            : _settingsService.JavaPath;

        var runtimes = await _javaService.GetInstalledRuntimesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(configuredJava))
        {
            var executable = JavaService.ResolveJavaExecutable(configuredJava);
            if (executable is null)
            {
                return LaunchPreflightResult.JavaMissing(
                    requiredMajor,
                    useInstanceSettings
                        ? "实例设置中指定的 Java 路径无效，请重新选择 Java。"
                        : "设置中指定的 Java 路径无效，请重新选择 Java。");
            }

            var home = Path.GetDirectoryName(Path.GetDirectoryName(executable));
            var runtime = runtimes.FirstOrDefault(candidate =>
                string.Equals(
                    Path.GetFullPath(candidate.HomePath),
                    Path.GetFullPath(home ?? string.Empty),
                    StringComparison.OrdinalIgnoreCase));

            var version = runtime?.Version ?? JavaService.ReadJavaVersionFromPath(executable);
            var major = JavaService.ParseJavaMajor(version ?? string.Empty);
            return BuildResult(executable, version, major, requiredMajor, runtime?.Name);
        }

        var preferred = await _javaService.GetPreferredRuntimeForGameAsync(
            instance.GameVersion,
            cancellationToken);

        if (preferred is null)
        {
            return LaunchPreflightResult.JavaMissing(requiredMajor);
        }

        var preferredMajor = JavaService.ParseJavaMajor(preferred.Version);
        return BuildResult(
            preferred.PreferredExecutable,
            preferred.Version,
            preferredMajor,
            requiredMajor,
            preferred.Name);
    }

    private static LaunchPreflightResult BuildResult(
        string? path,
        string? version,
        int major,
        int requiredMajor,
        string? displayName)
    {
        if (major <= 0)
        {
            return LaunchPreflightResult.JavaMissing(
                requiredMajor,
                "无法识别当前 Java 的版本，请在设置中重新选择 Java。");
        }

        var matches = requiredMajor == 8
            ? major == 8
            : major >= requiredMajor;

        if (!matches)
        {
            return LaunchPreflightResult.JavaMismatch(
                path,
                version,
                major,
                requiredMajor,
                displayName);
        }

        return new LaunchPreflightResult(
            true,
            true,
            true,
            path,
            version,
            major,
            requiredMajor,
            displayName,
            null);
    }
}
