using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Downloads Adoptium Temurin JDKs from the selected source (Tsinghua TUNA
/// mirror or Adoptium), manages launcher-local Java runtimes, and discovers
/// Java installations already present on the device.
/// </summary>
public sealed class JavaService
{
    private const string AdoptiumApiBase = "https://api.adoptium.net/v3/assets/latest";
    private const string TunaMirrorBase = "https://mirrors.tuna.tsinghua.edu.cn/Adoptium";

    private static readonly HttpClient HttpClient = CreateHttpClient();

    public JavaService(string? javaRoot = null)
    {
        JavaRoot = string.IsNullOrWhiteSpace(javaRoot)
            ? AppPaths.JavaDirectory
            : Path.GetFullPath(javaRoot);
    }

    public string JavaRoot { get; }

    public Task<JavaRelease> GetLatestReleaseAsync(
        int featureVersion,
        CancellationToken cancellationToken = default) =>
        GetLatestReleaseAsync(featureVersion, "TUNA", cancellationToken);

    public async Task<JavaRelease> GetLatestReleaseAsync(
        int featureVersion,
        string? source,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(source, "Adoptium", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                return await GetLatestReleaseFromAdoptiumAsync(featureVersion, timeout.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    "Adoptium 官方源当前不可用。请检查网络或代理，或把 Java 下载源切换为“清华 TUNA 镜像”。",
                    exception);
            }
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            return await GetLatestReleaseFromTunaAsync(featureVersion, timeout.Token)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "清华 TUNA 镜像当前不可用。请稍后重试，或把 Java 下载源切换为“Adoptium 官方源”。",
                exception);
        }
    }

    private async Task<JavaRelease> GetLatestReleaseFromTunaAsync(
        int featureVersion,
        CancellationToken cancellationToken)
    {
        var architecture = GetArchitecture();
        var directoryUrl = $"{TunaMirrorBase}/{featureVersion}/jdk/{architecture}/windows/";

        using var response = await HttpClient.GetAsync(
            directoryUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var packageName = Regex.Matches(html, "href=\"(?<file>OpenJDK[^\"]+\\.zip)\"")
            .Select(match => WebUtility.HtmlDecode(match.Groups["file"].Value))
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(file => VersionSortKey(file), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(packageName))
        {
            throw new InvalidOperationException("清华镜像中没有找到可用的 Java 压缩包。");
        }

        var downloadUrl = new Uri(new Uri(directoryUrl), packageName).AbsoluteUri;
        var semanticVersion = packageName
            .Replace(".zip", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Split("hotspot_", StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault()
            ?.Replace('_', '+')
            ?? featureVersion.ToString();

        return new JavaRelease(
            featureVersion,
            Path.GetFileNameWithoutExtension(packageName),
            semanticVersion,
            packageName,
            downloadUrl,
            null,
            0);
    }

    private async Task<JavaRelease> GetLatestReleaseFromAdoptiumAsync(
        int featureVersion,
        CancellationToken cancellationToken)
    {
        var architecture = GetArchitecture();
        var requestUrl =
            $"{AdoptiumApiBase}/{featureVersion}/hotspot" +
            $"?architecture={architecture}&image_type=jdk&os=windows&vendor=eclipse";

        using var response = await HttpClient
            .GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException(
                $"Adoptium 没有找到适用于 Windows {architecture} 的 Java {featureVersion} 构建。");
        }

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using var document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Adoptium 没有返回 Java {featureVersion} 的可用版本。");
        }

        var asset = root[0];
        var binary = GetProperty(asset, "binary");
        var package = GetProperty(binary, "package");
        var version = GetProperty(asset, "version");

        var downloadUrl = GetString(package, "link");
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Adoptium 返回了无效的下载地址。");
        }

        var releaseName = GetOptionalString(asset, "release_name") ?? $"jdk-{featureVersion}";
        var semanticVersion = GetOptionalString(version, "openjdk_version")
            ?? GetOptionalString(version, "semver")
            ?? releaseName;

        return new JavaRelease(
            featureVersion,
            releaseName,
            semanticVersion,
            GetString(package, "name"),
            downloadUrl,
            GetOptionalString(package, "checksum"),
            GetInt64(package, "size"));
    }

    public async Task<JavaRuntime> InstallAsync(
        int featureVersion,
        IProgress<JavaDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        string? source = null)
    {
        var release = await GetLatestReleaseAsync(featureVersion, source, cancellationToken)
            .ConfigureAwait(false);
        return await DownloadAndExtractAsync(release, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<JavaRuntime> DownloadAndExtractAsync(
        JavaRelease release,
        IProgress<JavaDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        AppPaths.EnsureDirectories();

        var directoryName = GetInstallDirectoryName(release);
        var installDirectory = Path.Combine(JavaRoot, directoryName);
        var javaExecutable = Path.Combine(installDirectory, "bin", "java.exe");

        if (File.Exists(javaExecutable))
        {
            progress?.Report(new JavaDownloadProgress(100, "该版本已经安装。"));
            return new JavaRuntime(directoryName, release.SemanticVersion, installDirectory);
        }

        var archivePath = Path.Combine(
            Path.GetTempPath(),
            $"lmcl-java-{Guid.NewGuid():N}{Path.GetExtension(release.PackageName)}");

        if (string.IsNullOrWhiteSpace(Path.GetExtension(archivePath)))
        {
            archivePath += ".zip";
        }

        var stagingDirectory = Path.Combine(JavaRoot, $".staging-{Guid.NewGuid():N}");

        try
        {
            progress?.Report(new JavaDownloadProgress(0, "正在连接 Java 下载服务器..."));
            await DownloadFileAsync(release, archivePath, progress, cancellationToken)
                .ConfigureAwait(false);
            await VerifyChecksumAsync(archivePath, release.Sha256, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(new JavaDownloadProgress(0, "正在解压 Java 运行时..."));
            Directory.CreateDirectory(stagingDirectory);
            ZipFile.ExtractToDirectory(archivePath, stagingDirectory, overwriteFiles: true);

            var extractedHome = FindJdkHome(stagingDirectory);
            if (Directory.Exists(installDirectory))
            {
                Directory.Delete(installDirectory, recursive: true);
            }

            Directory.Move(extractedHome, installDirectory);

            progress?.Report(new JavaDownloadProgress(100, "Java 安装完成。"));
            return new JavaRuntime(directoryName, release.SemanticVersion, installDirectory);
        }
        finally
        {
            TryDeleteFile(archivePath);
            TryDeleteDirectory(stagingDirectory);
        }
    }

    public Task<IReadOnlyList<JavaRuntime>> GetInstalledRuntimesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<JavaRuntime>>(() =>
        {
            var runtimes = new Dictionary<string, JavaRuntime>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in GetJavaSearchRoots())
            {
                cancellationToken.ThrowIfCancellationRequested();
                ScanJavaRoot(root, ShouldDeepScan(root), runtimes, cancellationToken);
            }

            AddJavaHome(Environment.GetEnvironmentVariable("JAVA_HOME"), runtimes);
            AddJavaHomesFromPath(runtimes);

            foreach (var directory in GetConfiguredRuntimeDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                ScanJavaRoot(directory, true, runtimes, cancellationToken);
            }

            return runtimes.Values
                .OrderByDescending(runtime => ParseJavaMajor(runtime.Version))
                .ThenBy(runtime => runtime.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    public async Task<JavaRuntime?> GetPreferredRuntimeAsync(
        CancellationToken cancellationToken = default)
    {
        var runtimes = await GetInstalledRuntimesAsync(cancellationToken).ConfigureAwait(false);
        return runtimes
            .OrderByDescending(runtime => ParseJavaMajor(runtime.Version))
            .ThenByDescending(runtime => runtime.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public async Task<JavaRuntime?> GetPreferredRuntimeForGameAsync(
        string gameVersion,
        CancellationToken cancellationToken = default)
    {
        var runtimes = await GetInstalledRuntimesAsync(cancellationToken).ConfigureAwait(false);
        if (runtimes.Count == 0)
        {
            return null;
        }

        var requiredMajor = GetRequiredJavaMajor(gameVersion);
        var exact = runtimes
            .Where(runtime => ParseJavaMajor(runtime.Version) == requiredMajor)
            .OrderByDescending(runtime => runtime.Version, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (exact is not null)
        {
            return exact;
        }

        return runtimes
                   .Where(runtime => ParseJavaMajor(runtime.Version) >= requiredMajor)
                   .OrderBy(runtime => ParseJavaMajor(runtime.Version))
                   .FirstOrDefault()
               ?? runtimes.OrderByDescending(runtime => ParseJavaMajor(runtime.Version)).FirstOrDefault();
    }

    public static int GetRequiredJavaMajor(string gameVersion)
    {
        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return 21;
        }

        var text = gameVersion.Trim();
        if (text.StartsWith("1.", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var minor))
            {
                return minor <= 16 ? 8 : minor <= 20 ? 17 : 21;
            }

            return 8;
        }

        if (int.TryParse(text.Split('.', '-')[0], out var major))
        {
            return major >= 20 ? 21 : 17;
        }

        return 21;
    }

    public static int ParseJavaMajor(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return 0;
        }

        var normalized = version.Trim();
        if (normalized.StartsWith("1.", StringComparison.OrdinalIgnoreCase))
        {
            var legacyParts = normalized.Split('.');
            return legacyParts.Length >= 2 && int.TryParse(legacyParts[1], out var legacyMajor)
                ? legacyMajor
                : 0;
        }

        var firstPart = normalized.Split('.', '-', '+')[0];
        return int.TryParse(firstPart, out var major) ? major : 0;
    }

    public static string? ResolveJavaExecutable(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return null;
        }

        var path = configuredPath.Trim().Trim('"');

        if (Directory.Exists(path))
        {
            var javaw = Path.Combine(path, "bin", "javaw.exe");
            if (File.Exists(javaw))
            {
                return javaw;
            }

            var java = Path.Combine(path, "bin", "java.exe");
            if (File.Exists(java))
            {
                return java;
            }
        }

        if (File.Exists(path) &&
            Path.GetFileName(path).StartsWith("java", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return null;
    }

    public static string? ReadJavaVersionFromPath(string? javaExecutable)
    {
        if (string.IsNullOrWhiteSpace(javaExecutable))
        {
            return null;
        }

        var binDirectory = Path.GetDirectoryName(javaExecutable);
        var homeDirectory = binDirectory is null ? null : Path.GetDirectoryName(binDirectory);
        if (string.IsNullOrWhiteSpace(homeDirectory))
        {
            return null;
        }

        return ReadReleaseVersion(Path.Combine(homeDirectory, "release"));
    }

    private static IEnumerable<string> GetJavaSearchRoots()
    {
        var roots = new List<string> { AppPaths.JavaDirectory };
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        foreach (var baseDirectory in new[] { programFiles, programFilesX86 })
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                continue;
            }

            foreach (var vendor in new[]
                     {
                         "Java",
                         "Eclipse Adoptium",
                         "Microsoft",
                         "Zulu",
                         "Amazon Corretto",
                         "BellSoft",
                         "Semeru",
                         "AdoptOpenJDK",
                         "OpenJDK",
                     })
            {
                roots.Add(Path.Combine(baseDirectory, vendor));
            }
        }

        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            roots.Add(Path.Combine(localAppData, "Programs", "Eclipse Adoptium"));
            roots.Add(Path.Combine(localAppData, "Programs", "Microsoft"));
        }

        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            roots.Add(Path.Combine(userProfile, ".jdks"));
        }

        if (!string.IsNullOrWhiteSpace(appData))
        {
            roots.Add(Path.Combine(appData, ".minecraft", "runtime"));
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetConfiguredRuntimeDirectories()
    {
        var configured = Environment.GetEnvironmentVariable("LITMC_JAVA_DIRS");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return [];
        }

        return configured.Split(
            Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool ShouldDeepScan(string root)
    {
        return root.EndsWith(Path.Combine(".minecraft", "runtime"), StringComparison.OrdinalIgnoreCase) ||
               root.EndsWith(".jdks", StringComparison.OrdinalIgnoreCase);
    }

    private static void ScanJavaRoot(
        string root,
        bool deep,
        Dictionary<string, JavaRuntime> runtimes,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
            AddJavaHome(root, runtimes);

            foreach (var directory in Directory.EnumerateDirectories(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = Path.GetFileName(directory);
                if (!name.StartsWith('.'))
                {
                    AddJavaHome(directory, runtimes);
                }
            }

            if (deep)
            {
                foreach (var javaExecutable in Directory.EnumerateFiles(
                             root, "java.exe", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var binDirectory = Path.GetDirectoryName(javaExecutable);
                    var home = binDirectory is null ? null : Path.GetDirectoryName(binDirectory);
                    if (home is not null)
                    {
                        AddJavaHome(home, runtimes);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void AddJavaHome(string? home, Dictionary<string, JavaRuntime> runtimes)
    {
        if (string.IsNullOrWhiteSpace(home))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(home.Trim().Trim('"'));
            if (!File.Exists(Path.Combine(fullPath, "bin", "java.exe")))
            {
                return;
            }

            runtimes.TryAdd(
                fullPath,
                new JavaRuntime(
                    Path.GetFileName(fullPath),
                    ReadReleaseVersion(Path.Combine(fullPath, "release")) ?? Path.GetFileName(fullPath),
                    fullPath));
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
        }
    }

    private static void AddJavaHomesFromPath(Dictionary<string, JavaRuntime> runtimes)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        foreach (var entry in path.Split(
                     Path.PathSeparator,
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var javaExecutable = Path.Combine(entry.Trim('"'), "java.exe");
                if (!File.Exists(javaExecutable))
                {
                    continue;
                }

                var binDirectory = Path.GetDirectoryName(javaExecutable);
                var home = binDirectory is null ? null : Path.GetDirectoryName(binDirectory);
                AddJavaHome(home, runtimes);
            }
            catch (ArgumentException)
            {
            }
        }
    }

    private static async Task DownloadFileAsync(
        JavaRelease release,
        string destinationPath,
        IProgress<JavaDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient
            .GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? release.Size;
        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long downloadedBytes = 0;
        var lastReportedPercent = -1d;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                .ConfigureAwait(false);
            downloadedBytes += read;

            if (totalBytes <= 0)
            {
                continue;
            }

            var percent = downloadedBytes * 100d / totalBytes;
            if (percent - lastReportedPercent < 0.5 && percent < 100)
            {
                continue;
            }

            lastReportedPercent = percent;
            progress?.Report(new JavaDownloadProgress(
                percent,
                $"正在下载 {downloadedBytes / 1024d / 1024d:F1} MB / {totalBytes / 1024d / 1024d:F1} MB"));
        }

        progress?.Report(new JavaDownloadProgress(100, "下载完成，正在校验文件..."));
    }

    private static async Task VerifyChecksumAsync(
        string filePath,
        string? expectedChecksum,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedChecksum))
        {
            return;
        }

        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        var actualChecksum = Convert.ToHexString(hash);

        if (!actualChecksum.Equals(expectedChecksum.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Java 安装包校验失败，文件可能不完整。");
        }
    }

    private static string FindJdkHome(string extractionRoot)
    {
        if (File.Exists(Path.Combine(extractionRoot, "bin", "java.exe")))
        {
            return extractionRoot;
        }

        var candidates = Directory
            .EnumerateDirectories(extractionRoot)
            .Where(directory => File.Exists(Path.Combine(directory, "bin", "java.exe")))
            .ToList();

        return candidates.Count == 1
            ? candidates[0]
            : throw new InvalidDataException("无法在下载的压缩包中找到 Java 运行时目录。");
    }

    private static string? ReadReleaseVersion(string releaseFilePath)
    {
        if (!File.Exists(releaseFilePath))
        {
            return null;
        }

        try
        {
            foreach (var line in File.ReadLines(releaseFilePath))
            {
                const string prefix = "JAVA_VERSION=";
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return line[prefix.Length..].Trim().Trim('"');
                }
            }
        }
        catch (IOException)
        {
        }

        return null;
    }

    private static string GetInstallDirectoryName(JavaRelease release)
    {
        var name = string.IsNullOrWhiteSpace(release.ReleaseName)
            ? $"temurin-{release.FeatureVersion}"
            : release.ReleaseName;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(character =>
            invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();

        return string.IsNullOrWhiteSpace(sanitized)
            ? $"temurin-{release.FeatureVersion}"
            : sanitized;
    }

    private static string VersionSortKey(string fileName)
    {
        var match = Regex.Match(
            fileName,
            @"hotspot_(?<version>\d+(?:[._]\d+)*)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return fileName;
        }

        var parts = Regex.Split(match.Groups["version"].Value, @"[._]")
            .Select(part => int.TryParse(part, out var value) ? value.ToString("D6") : part)
            .ToArray();

        return string.Join(".", parts);
    }

    private static JsonElement GetProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property
            : throw new InvalidOperationException($"Adoptium 响应缺少字段：{propertyName}。");
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        var property = GetProperty(element, propertyName);
        return property.GetString()
            ?? throw new InvalidOperationException($"Adoptium 响应字段为空：{propertyName}。");
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static long GetInt64(JsonElement element, string propertyName)
    {
        var property = GetProperty(element, propertyName);
        return property.TryGetInt64(out var value) ? value : 0;
    }

    private static string GetArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "aarch64",
            Architecture.X86 => "x86",
            _ => "x64",
        };
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("LiteMCLauncher/0.1.0");
        return client;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
