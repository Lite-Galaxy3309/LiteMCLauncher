using System.IO.Compression;
using System.Text.Json;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Installs a Modrinth .mrpack or CurseForge modpack into a new loader instance:
/// installs the required loader, downloads every declared file, copies the
/// overrides and selects the resulting instance.
/// </summary>
public sealed class ModpackInstallService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ModPlatformService _platformService;
    private readonly ModLoaderInstallService _loaderService;
    private readonly MinecraftInstanceService _instanceService;
    private readonly LauncherState _launcherState;
    private readonly LauncherSettingsService _settingsService;
    private readonly SemaphoreSlim _installGate = new(1, 1);

    public ModpackInstallService()
        : this(
            AppServices.Mods,
            AppServices.Loaders,
            AppServices.Instances,
            AppServices.LauncherState,
            AppServices.Settings)
    {
    }

    public ModpackInstallService(
        ModPlatformService platformService,
        ModLoaderInstallService loaderService,
        MinecraftInstanceService instanceService,
        LauncherState launcherState,
        LauncherSettingsService settingsService)
    {
        _platformService = platformService;
        _loaderService = loaderService;
        _instanceService = instanceService;
        _launcherState = launcherState;
        _settingsService = settingsService;
    }

    public async Task<ModpackInstallResult> InstallAsync(
        ModSearchResult project,
        IProgress<ModpackInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (!project.IsModpack)
        {
            throw new InvalidOperationException("该项目不是整合包。");
        }

        await _installGate.WaitAsync(cancellationToken);
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"lmcl-modpack-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(tempDirectory);
            Report(progress, 0, "正在下载整合包文件...");

            var archiveProgress = new CallbackProgress<ModDownloadProgress>(download =>
                Report(progress, download.Percentage * 0.1, download.Status));

            var archivePath = await _platformService.DownloadModpackArchiveAsync(
                project,
                tempDirectory,
                _settingsService.CurseForgeApiKey,
                archiveProgress,
                cancellationToken);

            Report(progress, 10, "正在解析整合包...");

            var extractionDirectory = Path.Combine(tempDirectory, "extracted");
            Directory.CreateDirectory(extractionDirectory);
            ZipFile.ExtractToDirectory(archivePath, extractionDirectory, overwriteFiles: true);

            var manifest = await ReadManifestAsync(
                extractionDirectory,
                project,
                cancellationToken);

            Report(
                progress,
                15,
                $"正在安装 Minecraft {manifest.MinecraftVersion} · {manifest.LoaderDisplay}...");

            var loaderProgress = new CallbackProgress<MinecraftInstallProgress>(install =>
                Report(progress, 15 + install.Percentage * 0.35, install.Status));

            var loader = await _loaderService.InstallAsync(
                manifest.MinecraftVersion,
                manifest.LoaderType,
                manifest.LoaderVersion,
                loaderProgress,
                cancellationToken);

            var instance = await GetOrCreateInstanceAsync(loader, manifest, cancellationToken);
            _launcherState.SelectedInstance = instance;
            _launcherState.EnsureInstanceDirectories(instance);

            var gameDirectory = _launcherState.GetGameDirectory(instance);
            Report(progress, 50, "正在下载整合包内容...");

            var fileCount = manifest.Kind switch
            {
                ModpackKind.Modrinth => await InstallModrinthFilesAsync(
                    manifest,
                    gameDirectory,
                    progress,
                    cancellationToken),
                ModpackKind.CurseForge => await InstallCurseForgeFilesAsync(
                    manifest,
                    gameDirectory,
                    progress,
                    cancellationToken),
                _ => 0,
            };

            Report(progress, 96, "正在复制整合包配置与资源...");
            CopyOverrides(extractionDirectory, manifest.OverridesFolder, gameDirectory);

            Report(progress, 100, "整合包安装完成。");
            return new ModpackInstallResult(
                instance.Id,
                manifest.Name,
                fileCount,
                loader.LoaderDisplay);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
            _installGate.Release();
        }
    }

    private static async Task<ModpackManifest> ReadManifestAsync(
        string extractionDirectory,
        ModSearchResult project,
        CancellationToken cancellationToken)
    {
        var modrinthIndexPath = Path.Combine(extractionDirectory, "modrinth.index.json");
        if (File.Exists(modrinthIndexPath))
        {
            await using var stream = File.OpenRead(modrinthIndexPath);
            var dto = await JsonSerializer.DeserializeAsync<ModrinthIndexDto>(
                stream, JsonOptions, cancellationToken);

            if (dto?.Dependencies is null)
            {
                throw new InvalidOperationException("Modrinth 整合包缺少 dependencies 信息。");
            }

            if (!dto.Dependencies.TryGetValue("minecraft", out var minecraftVersion) ||
                string.IsNullOrWhiteSpace(minecraftVersion))
            {
                throw new InvalidOperationException("Modrinth 整合包没有指定 Minecraft 版本。");
            }

            var (loaderType, loaderVersion) = ParseModrinthLoader(dto.Dependencies);
            return new ModpackManifest
            {
                Kind = ModpackKind.Modrinth,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? project.Title : dto.Name,
                MinecraftVersion = minecraftVersion,
                LoaderType = loaderType,
                LoaderVersion = loaderVersion,
                OverridesFolder = "overrides",
                ModrinthFiles = dto.Files ?? [],
            };
        }

        var curseManifestPath = Path.Combine(extractionDirectory, "manifest.json");
        if (File.Exists(curseManifestPath))
        {
            await using var stream = File.OpenRead(curseManifestPath);
            var dto = await JsonSerializer.DeserializeAsync<CurseManifestDto>(
                stream, JsonOptions, cancellationToken);

            if (string.IsNullOrWhiteSpace(dto?.Minecraft?.Version))
            {
                throw new InvalidOperationException("CurseForge 整合包没有指定 Minecraft 版本。");
            }

            var (loaderType, loaderVersion) = ParseCurseForgeLoader(dto.Minecraft.ModLoaders);
            return new ModpackManifest
            {
                Kind = ModpackKind.CurseForge,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? project.Title : dto.Name,
                MinecraftVersion = dto.Minecraft.Version,
                LoaderType = loaderType,
                LoaderVersion = loaderVersion,
                OverridesFolder = string.IsNullOrWhiteSpace(dto.Overrides) ? "overrides" : dto.Overrides,
                CurseFiles = dto.Files ?? [],
            };
        }

        throw new InvalidOperationException("无法识别整合包格式：需要 modrinth.index.json 或 manifest.json。");
    }

    private static (ModLoaderType Type, string? Version) ParseModrinthLoader(
        IReadOnlyDictionary<string, string> dependencies)
    {
        if (dependencies.TryGetValue("fabric-loader", out var fabric))
        {
            return (ModLoaderType.Fabric, fabric);
        }

        if (dependencies.TryGetValue("quilt-loader", out var quilt))
        {
            return (ModLoaderType.Quilt, quilt);
        }

        if (dependencies.TryGetValue("neoforge", out var neoForge))
        {
            return (ModLoaderType.NeoForge, neoForge);
        }

        if (dependencies.TryGetValue("forge", out var forge))
        {
            return (ModLoaderType.Forge, forge);
        }

        return (ModLoaderType.Vanilla, null);
    }

    private static (ModLoaderType Type, string? Version) ParseCurseForgeLoader(
        IReadOnlyList<CurseModLoaderDto>? modLoaders)
    {
        var loader = modLoaders?.FirstOrDefault(item => item.Primary)
                     ?? modLoaders?.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(loader?.Id))
        {
            return (ModLoaderType.Vanilla, null);
        }

        var value = loader.Id;
        if (value.StartsWith("fabric-", StringComparison.OrdinalIgnoreCase))
        {
            return (ModLoaderType.Fabric, value["fabric-".Length..]);
        }

        if (value.StartsWith("quilt-", StringComparison.OrdinalIgnoreCase))
        {
            return (ModLoaderType.Quilt, value["quilt-".Length..]);
        }

        if (value.StartsWith("neoforge-", StringComparison.OrdinalIgnoreCase))
        {
            return (ModLoaderType.NeoForge, value["neoforge-".Length..]);
        }

        if (value.StartsWith("forge-", StringComparison.OrdinalIgnoreCase))
        {
            return (ModLoaderType.Forge, value["forge-".Length..]);
        }

        return (ModLoaderType.Vanilla, null);
    }

    private async Task<MinecraftInstance> GetOrCreateInstanceAsync(
        ModLoaderInstallResult loader,
        ModpackManifest manifest,
        CancellationToken cancellationToken)
    {
        var instances = await _instanceService.GetInstancesAsync(cancellationToken);
        var instance = instances.FirstOrDefault(item => item.Id == loader.VersionId);
        if (instance is not null)
        {
            return instance;
        }

        return new MinecraftInstance(
            loader.VersionId,
            Path.Combine(AppPaths.VersionsDirectory, loader.VersionId),
            manifest.MinecraftVersion,
            "custom",
            loader.LoaderDisplay,
            DateTimeOffset.Now);
    }

    private async Task<int> InstallModrinthFilesAsync(
        ModpackManifest manifest,
        string gameDirectory,
        IProgress<ModpackInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var files = manifest.ModrinthFiles
            .Where(file => !string.Equals(file.Env?.Client, "unsupported", StringComparison.OrdinalIgnoreCase))
            .Where(file => !string.IsNullOrWhiteSpace(file.Path))
            .ToList();

        var completed = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var targetPath = ResolveSafePath(gameDirectory, file.Path!);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            var urls = file.Downloads?.Where(url => !string.IsNullOrWhiteSpace(url)).ToList() ?? [];
            if (urls.Count == 0)
            {
                continue;
            }

            var percentage = files.Count == 0
                ? 95
                : 50 + completed * 45d / files.Count;
            Report(progress, percentage, $"下载 {Path.GetFileName(targetPath)}...");

            Exception? lastError = null;
            var downloaded = false;

            foreach (var url in urls)
            {
                try
                {
                    await DownloadFileAsync(
                        url,
                        targetPath,
                        file.FileSize,
                        cancellationToken);
                    downloaded = true;
                    break;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    lastError = exception;
                }
            }

            if (!downloaded)
            {
                throw new InvalidOperationException(
                    $"整合包文件 {file.Path} 下载失败：{lastError?.Message}",
                    lastError);
            }

            completed++;
        }

        return completed;
    }

    private async Task<int> InstallCurseForgeFilesAsync(
        ModpackManifest manifest,
        string gameDirectory,
        IProgress<ModpackInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        var apiKey = _settingsService.CurseForgeApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "安装 CurseForge 整合包需要 API Key，请先在 Mods 页面配置。");
        }

        var modsDirectory = Path.Combine(gameDirectory, "mods");
        Directory.CreateDirectory(modsDirectory);

        var files = manifest.CurseFiles.ToList();
        var completed = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var percentage = files.Count == 0
                ? 95
                : 50 + completed * 45d / files.Count;
            Report(progress, percentage, $"下载 CurseForge 文件 {completed + 1}/{files.Count}...");

            await _platformService.DownloadCurseForgeFileAsync(
                file.ProjectID.ToString(),
                file.FileID,
                modsDirectory,
                apiKey,
                progress: null,
                cancellationToken);

            completed++;
        }

        return completed;
    }

    private static void CopyOverrides(
        string extractionDirectory,
        string overridesFolder,
        string gameDirectory)
    {
        foreach (var folder in new[] { overridesFolder, "client-overrides" })
        {
            var sourceDirectory = Path.Combine(extractionDirectory, folder);
            if (!Directory.Exists(sourceDirectory))
            {
                continue;
            }

            CopyDirectory(sourceDirectory, gameDirectory);
        }
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(
                     sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            Directory.CreateDirectory(ResolveSafePath(destinationDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(
                     sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            var target = ResolveSafePath(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        long expectedSize,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await source.CopyToAsync(destination, cancellationToken);
        _ = expectedSize;
    }

    private static string ResolveSafePath(string rootDirectory, string relativePath)
    {
        var root = Path.GetFullPath(rootDirectory);
        var normalizedRelative = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var target = Path.GetFullPath(Path.Combine(root, normalizedRelative));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"整合包包含非法路径：{relativePath}");
        }

        return target;
    }

    private static void Report(
        IProgress<ModpackInstallProgress>? progress,
        double percentage,
        string status)
    {
        progress?.Report(new ModpackInstallProgress(
            Math.Clamp(percentage, 0, 100),
            status));
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private enum ModpackKind
    {
        Modrinth,
        CurseForge,
    }

    private sealed class ModpackManifest
    {
        public ModpackKind Kind { get; init; }
        public string Name { get; init; } = string.Empty;
        public string MinecraftVersion { get; init; } = string.Empty;
        public ModLoaderType LoaderType { get; init; }
        public string? LoaderVersion { get; init; }
        public string OverridesFolder { get; init; } = "overrides";
        public List<ModrinthIndexFileDto> ModrinthFiles { get; init; } = [];
        public List<CurseManifestFileDto> CurseFiles { get; init; } = [];

        public string LoaderDisplay => LoaderType switch
        {
            ModLoaderType.Fabric => "Fabric",
            ModLoaderType.Forge => "Forge",
            ModLoaderType.NeoForge => "NeoForge",
            ModLoaderType.Quilt => "Quilt",
            _ => "原版",
        };
    }

    private sealed class ModrinthIndexDto
    {
        public string? Name { get; set; }
        public string? VersionId { get; set; }
        public Dictionary<string, string>? Dependencies { get; set; }
        public List<ModrinthIndexFileDto>? Files { get; set; }
    }

    private sealed class ModrinthIndexFileDto
    {
        public string? Path { get; set; }
        public List<string>? Downloads { get; set; }
        public long FileSize { get; set; }
        public ModrinthEnvironmentDto? Env { get; set; }
    }

    private sealed class ModrinthEnvironmentDto
    {
        public string? Client { get; set; }
        public string? Server { get; set; }
    }

    private sealed class CurseManifestDto
    {
        public string? Name { get; set; }
        public string? Version { get; set; }
        public string? Overrides { get; set; }
        public CurseMinecraftDto? Minecraft { get; set; }
        public List<CurseManifestFileDto>? Files { get; set; }
    }

    private sealed class CurseMinecraftDto
    {
        public string? Version { get; set; }
        public List<CurseModLoaderDto>? ModLoaders { get; set; }
    }

    private sealed class CurseModLoaderDto
    {
        public string? Id { get; set; }
        public bool Primary { get; set; }
    }

    private sealed class CurseManifestFileDto
    {
        public int ProjectID { get; set; }
        public int FileID { get; set; }
        public bool Required { get; set; }
    }
}
