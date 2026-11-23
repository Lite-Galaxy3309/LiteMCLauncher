using CmlLib.Core;
using CmlLib.Core.FileExtractors;
using CmlLib.Core.VersionMetadata;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Installs Vanilla Minecraft versions and their assets/libraries with
/// CmlLib.Core. MinecraftLauncher.InstallAsync is the CmlLib 4.x replacement
/// for the older MinecraftInstaller type.
/// </summary>
public sealed class MinecraftInstallService
{
    private readonly MinecraftLauncher _launcher;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public MinecraftInstallService()
    {
        AppPaths.EnsureDirectories();
        var path = new MinecraftPath(AppPaths.MinecraftDirectory);
        var parameters = MinecraftLauncherParameters.CreateDefault(path);

        // Java is managed by JavaService and passed explicitly at launch time.
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

    public string MinecraftDirectory => _launcher.MinecraftPath.BasePath;

    public async Task<IReadOnlyList<MinecraftVersionInfo>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            VersionMetadataCollection metadata;
            if (_launcher.Versions is null)
            {
                metadata = await _launcher.GetAllVersionsAsync(cancellationToken);
            }
            else
            {
                metadata = _launcher.Versions;
            }

            return metadata
                .Select(version => new MinecraftVersionInfo(
                    version.Name,
                    version.Type,
                    version.ReleaseTime,
                    IsInstalled(version.Name)))
                .OrderByDescending(version => version.ReleaseTime)
                .ThenByDescending(version => version.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task InstallAsync(
        string versionName,
        IProgress<MinecraftInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionName);

        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var tracker = new InstallProgressTracker(progress);
            tracker.ReportStage($"正在读取 Minecraft {versionName} 的版本文件...");

            await _launcher.InstallAsync(
                versionName,
                tracker.FileProgress,
                tracker.ByteProgress,
                cancellationToken);

            tracker.ReportComplete($"Minecraft {versionName} 安装完成。");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public bool IsInstalled(string versionName)
    {
        var versionDirectory = Path.Combine(_launcher.MinecraftPath.Versions, versionName);
        return File.Exists(Path.Combine(versionDirectory, $"{versionName}.jar"));
    }
}
