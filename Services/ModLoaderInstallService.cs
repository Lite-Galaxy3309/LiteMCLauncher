using CmlLib.Core;
using CmlLib.Core.FileExtractors;
using CmlLib.Core.Installer.Forge.Versions;
using CmlLib.Core.ModLoaders.FabricMC;
using CmlLib.Core.ModLoaders.QuiltMC;
using LiteMCLauncher.Models;
using ForgeInstallerApi = CmlLib.Core.Installer.Forge.ForgeInstaller;
using ForgeInstallOptionsApi = CmlLib.Core.Installer.Forge.ForgeInstallOptions;
using NeoForgeInstallerApi = CmlLib.Core.Installer.NeoForge.NeoForgeInstaller;
using NeoForgeVersionApi = CmlLib.Core.Installer.Forge.Versions.NeoForgeVersion;

namespace LiteMCLauncher.Services;

/// <summary>
/// Installs Forge, NeoForge, Fabric or Quilt on top of a Vanilla version.
/// </summary>
public sealed class ModLoaderInstallService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(20),
    };

    private readonly MinecraftLauncher _launcher;
    private readonly SemaphoreSlim _installGate = new(1, 1);

    public ModLoaderInstallService()
    {
        AppPaths.EnsureDirectories();
        var path = new MinecraftPath(AppPaths.MinecraftDirectory);
        var parameters = MinecraftLauncherParameters.CreateDefault(path);

        if (parameters.FileExtractors is { } extractors)
        {
            var javaExtractor = extractors.OfType<JavaFileExtractor>().FirstOrDefault();
            if (javaExtractor is not null)
            {
                extractors.Remove(javaExtractor);
            }
        }

        _launcher = new MinecraftLauncher(parameters);
    }

    public async Task<ModLoaderInstallResult> InstallAsync(
        string gameVersion,
        ModLoaderType loader,
        string? requestedLoaderVersion = null,
        IProgress<MinecraftInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _installGate.WaitAsync(cancellationToken);
        try
        {
            var tracker = new InstallProgressTracker(progress);
            tracker.ReportStage($"正在安装 Minecraft {gameVersion} 原版文件...");

            var version = await _launcher.GetVersionAsync(gameVersion, cancellationToken);
            await _launcher.InstallAsync(
                version,
                tracker.FileProgress,
                tracker.ByteProgress,
                cancellationToken);

            if (loader == ModLoaderType.Vanilla)
            {
                tracker.ReportComplete($"Minecraft {gameVersion} 原版安装完成。");
                return new ModLoaderInstallResult(version.Id, ModLoaderType.Vanilla, null);
            }

            tracker.ReportStage($"正在安装 {loader} 加载器...");

            return loader switch
            {
                ModLoaderType.Fabric => await InstallFabricAsync(gameVersion, requestedLoaderVersion, tracker, cancellationToken),
                ModLoaderType.Quilt => await InstallQuiltAsync(gameVersion, requestedLoaderVersion, tracker, cancellationToken),
                ModLoaderType.Forge => await InstallForgeAsync(gameVersion, requestedLoaderVersion, tracker, cancellationToken),
                ModLoaderType.NeoForge => await InstallNeoForgeAsync(gameVersion, requestedLoaderVersion, tracker, cancellationToken),
                _ => throw new InvalidOperationException($"不支持的加载器：{loader}"),
            };
        }
        finally
        {
            _installGate.Release();
        }
    }

    private async Task<ModLoaderInstallResult> InstallFabricAsync(
        string gameVersion,
        string? requestedLoaderVersion,
        InstallProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        var installer = new FabricInstaller(HttpClient);
        var loaders = await installer.GetLoaders(gameVersion);
        var loader = string.IsNullOrWhiteSpace(requestedLoaderVersion)
            ? loaders.FirstOrDefault()
            : loaders.FirstOrDefault(item => string.Equals(item.Version, requestedLoaderVersion, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(loader?.Version))
        {
            throw new InvalidOperationException(
                $"找不到适用于 Minecraft {gameVersion} 的 Fabric 加载器版本 {requestedLoaderVersion}。");
        }

        var versionId = await installer.Install(gameVersion, loader.Version, _launcher.MinecraftPath);
        tracker.ReportComplete($"Fabric {loader.Version} 安装完成。");
        return new ModLoaderInstallResult(versionId, ModLoaderType.Fabric, loader.Version);
    }

    private async Task<ModLoaderInstallResult> InstallQuiltAsync(
        string gameVersion,
        string? requestedLoaderVersion,
        InstallProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        var installer = new QuiltInstaller(HttpClient);
        var loaders = await installer.GetLoaders(gameVersion);
        var loader = string.IsNullOrWhiteSpace(requestedLoaderVersion)
            ? loaders.FirstOrDefault()
            : loaders.FirstOrDefault(item => string.Equals(item.Version, requestedLoaderVersion, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(loader?.Version))
        {
            throw new InvalidOperationException(
                $"找不到适用于 Minecraft {gameVersion} 的 Quilt 加载器版本 {requestedLoaderVersion}。");
        }

        var versionId = await installer.Install(gameVersion, loader.Version, _launcher.MinecraftPath);
        tracker.ReportComplete($"Quilt {loader.Version} 安装完成。");
        return new ModLoaderInstallResult(versionId, ModLoaderType.Quilt, loader.Version);
    }

    private async Task<ModLoaderInstallResult> InstallForgeAsync(
        string gameVersion,
        string? requestedLoaderVersion,
        InstallProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        var forge = new ForgeInstallerApi(_launcher, HttpClient);
        var versions = (await forge.GetForgeVersions(gameVersion)).ToList();
        if (versions.Count == 0)
        {
            throw new InvalidOperationException($"找不到适用于 Minecraft {gameVersion} 的 Forge 版本。");
        }

        var normalizedRequested = NormalizeLoaderVersion(requestedLoaderVersion, "forge-");
        var forgeVersion = !string.IsNullOrWhiteSpace(normalizedRequested)
            ? versions.FirstOrDefault(item => string.Equals(item.ForgeVersionName, normalizedRequested, StringComparison.OrdinalIgnoreCase))
            : null;

        forgeVersion ??= versions.FirstOrDefault(item => item.IsRecommendedVersion)
                         ?? versions.FirstOrDefault(item => item.IsLatestVersion)
                         ?? versions[0];

        if (!string.IsNullOrWhiteSpace(normalizedRequested) &&
            !string.Equals(forgeVersion.ForgeVersionName, normalizedRequested, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"找不到适用于 Minecraft {gameVersion} 的 Forge {normalizedRequested}。");
        }

        var javaPath = await GetJavaExecutableAsync(cancellationToken);
        var installer = new ForgeInstallerVersionMapper().CreateInstaller(forgeVersion);
        var options = new ForgeInstallOptionsApi
        {
            JavaPath = javaPath,
            FileProgress = tracker.FileProgress,
            ByteProgress = tracker.ByteProgress,
            CancellationToken = cancellationToken,
            SkipIfAlreadyInstalled = false,
        };

        await installer.Install(_launcher.MinecraftPath, _launcher.GameInstaller, options);
        tracker.ReportComplete($"Forge {forgeVersion.ForgeVersionName} 安装完成。");
        return new ModLoaderInstallResult(installer.VersionName, ModLoaderType.Forge, forgeVersion.ForgeVersionName);
    }

    private async Task<ModLoaderInstallResult> InstallNeoForgeAsync(
        string gameVersion,
        string? requestedLoaderVersion,
        InstallProgressTracker tracker,
        CancellationToken cancellationToken)
    {
        var neo = new NeoForgeInstallerApi(_launcher);
        var versions = (await neo.GetNeoForgeVersions(gameVersion)).ToList();
        if (versions.Count == 0)
        {
            throw new InvalidOperationException($"找不到适用于 Minecraft {gameVersion} 的 NeoForge 版本。");
        }

        var normalizedRequested = NormalizeLoaderVersion(requestedLoaderVersion, "neoforge-");
        var neoVersion = !string.IsNullOrWhiteSpace(normalizedRequested)
            ? versions.FirstOrDefault(item => string.Equals(item.NeoForgeVersionName, normalizedRequested, StringComparison.OrdinalIgnoreCase))
            : null;

        neoVersion ??= versions[0];

        if (!string.IsNullOrWhiteSpace(normalizedRequested) &&
            !string.Equals(neoVersion.NeoForgeVersionName, normalizedRequested, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"找不到适用于 Minecraft {gameVersion} 的 NeoForge {normalizedRequested}。");
        }

        var javaPath = await GetJavaExecutableAsync(cancellationToken);
        var options = new ForgeInstallOptionsApi
        {
            JavaPath = javaPath,
            FileProgress = tracker.FileProgress,
            ByteProgress = tracker.ByteProgress,
            CancellationToken = cancellationToken,
            SkipIfAlreadyInstalled = false,
        };

        var versionId = await neo.Install(neoVersion, options);
        tracker.ReportComplete($"NeoForge {neoVersion.NeoForgeVersionName} 安装完成。");
        return new ModLoaderInstallResult(versionId, ModLoaderType.NeoForge, neoVersion.NeoForgeVersionName);
    }

    private async Task<string> GetJavaExecutableAsync(CancellationToken cancellationToken)
    {
        var configured = AppServices.Settings.JavaPath;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = configured.Trim().Trim('"');
            var java = Directory.Exists(path) ? Path.Combine(path, "bin", "java.exe") : path;
            if (File.Exists(java))
            {
                return java;
            }
        }

        var runtime = await AppServices.Java.GetPreferredRuntimeAsync(cancellationToken);
        if (runtime is not null && File.Exists(runtime.JavaExecutable))
        {
            return runtime.JavaExecutable;
        }

        throw new InvalidOperationException(
            "安装 Forge/NeoForge 需要 Java。请在“设置 > Java 管理”中安装 Java 21 或 Java 17。");
    }

    private static string? NormalizeLoaderVersion(string? version, string prefix)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var trimmed = version.Trim();
        return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[prefix.Length..]
            : trimmed;
    }
}
