using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Searches Modrinth and CurseForge with their public REST APIs and downloads
/// a compatible file into the selected instance's mods folder.
/// </summary>
public sealed class ModPlatformService
{
    private const string ModrinthBaseUrl = "https://api.modrinth.com/v2";
    private const string CurseForgeBaseUrl = "https://api.curseforge.com/v1";
    private const int CurseForgeMinecraftGameId = 432;
    private const int CurseForgeModClassId = 6;
    private const int CurseForgeModpackClassId = 4471;

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<ModSearchResult>> SearchAsync(
        string source,
        string projectType,
        string query,
        string? curseForgeApiKey,
        CancellationToken cancellationToken = default)
    {
        return string.Equals(source, "CurseForge", StringComparison.OrdinalIgnoreCase)
            ? await SearchCurseForgeAsync(query, projectType, curseForgeApiKey, cancellationToken)
            : await SearchModrinthAsync(query, projectType, cancellationToken);
    }

    /// <summary>
    /// Paged Modrinth search with an optional category facet.
    /// </summary>
    public async Task<ModSearchPage> SearchModrinthPageAsync(
        string projectType,
        string query,
        string? categorySlug,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var facets = new List<string>
        {
            $"[\"project_type:{NormalizeProjectType(projectType)}\"]",
        };

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            facets.Add($"[\"categories:{categorySlug}\"]");
        }

        var facetJson = $"[{string.Join(",", facets)}]";
        var url = $"{ModrinthBaseUrl}/search" +
                  $"?query={Uri.EscapeDataString(query)}" +
                  $"&limit={limit}" +
                  $"&offset={offset}" +
                  "&index=relevance" +
                  $"&facets={Uri.EscapeDataString(facetJson)}";

        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessfulAsync(response, "Modrinth", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<ModrinthSearchResponse>(stream, JsonOptions, cancellationToken);

        var hits = result?.Hits is null
            ? []
            : result.Hits
                .Select(MapModrinthHit)
                .Where(hit => !string.IsNullOrWhiteSpace(hit.ProjectId))
                .ToList();

        return new ModSearchPage(
            hits,
            result?.Offset ?? offset,
            result?.Limit ?? limit,
            result?.TotalHits ?? hits.Count);
    }

    public async Task<ModInstallResult> InstallAsync(
        ModSearchResult project,
        MinecraftInstance instance,
        string targetDirectory,
        string? curseForgeApiKey,
        IProgress<ModDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(instance);

        Directory.CreateDirectory(targetDirectory);

        return string.Equals(project.Source, "CurseForge", StringComparison.OrdinalIgnoreCase)
            ? await InstallCurseForgeAsync(project, instance, targetDirectory, curseForgeApiKey, progress, cancellationToken)
            : await InstallModrinthAsync(project, instance, targetDirectory, progress, cancellationToken);
    }

    /// <summary>
    /// Returns the full version list for a project, used by the details page.
    /// </summary>
    public async Task<IReadOnlyList<ModVersionInfo>> GetVersionsAsync(
        ModSearchResult project,
        string? curseForgeApiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        return string.Equals(project.Source, "CurseForge", StringComparison.OrdinalIgnoreCase)
            ? await GetCurseForgeVersionsAsync(project, curseForgeApiKey, cancellationToken)
            : await GetModrinthVersionsAsync(project, cancellationToken);
    }

    /// <summary>
    /// Downloads the latest modpack archive into a temporary/target directory.
    /// </summary>
    public async Task<string> DownloadModpackArchiveAsync(
        ModSearchResult project,
        string targetDirectory,
        string? curseForgeApiKey,
        IProgress<ModDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var versions = await GetVersionsAsync(project, curseForgeApiKey, cancellationToken);
        var version = versions.FirstOrDefault(item =>
                          item.FileName?.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase) == true)
                      ?? versions.FirstOrDefault(item =>
                          item.FileName?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)
                      ?? versions.FirstOrDefault();

        if (version is null)
        {
            throw new InvalidOperationException("该项目没有可下载的整合包文件。");
        }

        return await DownloadVersionAsync(
            project,
            version,
            targetDirectory,
            curseForgeApiKey,
            progress,
            cancellationToken);
    }

    /// <summary>
    /// Downloads a specific CurseForge file by project/file id (used by the
    /// CurseForge modpack manifest, which does not contain download URLs).
    /// </summary>
    public async Task<string> DownloadCurseForgeFileAsync(
        string projectId,
        long fileId,
        string targetDirectory,
        string apiKey,
        IProgress<ModDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("下载 CurseForge 整合包文件需要 API Key。");
        }

        var file = await GetCurseForgeFileAsync(projectId, fileId, apiKey, cancellationToken);
        var downloadUrl = file.DownloadUrl;
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            downloadUrl = await GetCurseForgeDownloadUrlAsync(
                projectId,
                fileId,
                apiKey,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException($"CurseForge 文件 {fileId} 未开放第三方下载。");
        }

        Directory.CreateDirectory(targetDirectory);
        var fileName = GetSafeFileName(file.FileName, $"mod-{fileId}.jar", false);
        var filePath = Path.Combine(targetDirectory, fileName);

        await DownloadFileAsync(
            downloadUrl,
            filePath,
            file.FileLength,
            progress,
            cancellationToken);

        return filePath;
    }

    private static async Task<CurseForgeFileDto> GetCurseForgeFileAsync(
        string projectId,
        long fileId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url = $"{CurseForgeBaseUrl}/mods/{Uri.EscapeDataString(projectId)}/files/{fileId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessfulAsync(response, "CurseForge", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<CurseForgeSingleResponse<CurseForgeFileDto>>(
            stream, JsonOptions, cancellationToken);

        return result?.Data ?? throw new InvalidOperationException($"找不到 CurseForge 文件 {fileId}。");
    }

    /// <summary>
    /// Loads gallery/screenshot image URLs for the details page.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetGalleryAsync(
        ModSearchResult project,
        string? curseForgeApiKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.Equals(project.Source, "CurseForge", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(curseForgeApiKey))
                {
                    return [];
                }

                var url = $"{CurseForgeBaseUrl}/mods/{Uri.EscapeDataString(project.ProjectId)}";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("x-api-key", curseForgeApiKey);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                using var response = await HttpClient.SendAsync(request, cancellationToken);
                await EnsureSuccessfulAsync(response, "CurseForge", cancellationToken);

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var result = await JsonSerializer.DeserializeAsync<CurseForgeSingleResponse<CurseForgeModDto>>(
                    stream, JsonOptions, cancellationToken);

                return result?.Data?.Screenshots?
                    .Select(screenshot => screenshot.ThumbnailUrl ?? screenshot.Url)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Select(url => url!)
                    .Take(10)
                    .ToList() ?? [];
            }

            var modrinthUrl = $"{ModrinthBaseUrl}/project/{Uri.EscapeDataString(project.ProjectId)}";
            using var modrinthResponse = await HttpClient.GetAsync(
                modrinthUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await EnsureSuccessfulAsync(modrinthResponse, "Modrinth", cancellationToken);

            await using var modrinthStream = await modrinthResponse.Content.ReadAsStreamAsync(cancellationToken);
            var modrinthProject = await JsonSerializer.DeserializeAsync<ModrinthProjectDto>(
                modrinthStream, JsonOptions, cancellationToken);

            return modrinthProject?.Gallery?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Take(10)
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Downloads a specific version selected on the details page.
    /// </summary>
    public async Task<string> DownloadVersionAsync(
        ModSearchResult project,
        ModVersionInfo version,
        string targetDirectory,
        string? curseForgeApiKey,
        IProgress<ModDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(version);

        Directory.CreateDirectory(targetDirectory);

        var downloadUrl = version.DownloadUrl;
        if (string.IsNullOrWhiteSpace(downloadUrl) &&
            string.Equals(project.Source, "CurseForge", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(curseForgeApiKey))
            {
                throw new InvalidOperationException("未配置 CurseForge API Key，无法解析下载地址。");
            }

            if (!long.TryParse(version.Id, out var fileId))
            {
                throw new InvalidOperationException("CurseForge 文件 ID 无效。");
            }

            downloadUrl = await GetCurseForgeDownloadUrlAsync(
                project.ProjectId,
                fileId,
                curseForgeApiKey,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException("该项目没有可用的下载地址。");
        }

        var fileName = GetSafeFileName(
            version.FileName,
            $"{project.Title}-{version.VersionNumber}",
            project.IsModpack);
        var filePath = Path.Combine(targetDirectory, fileName);

        await DownloadFileAsync(
            downloadUrl,
            filePath,
            version.FileSize,
            progress,
            cancellationToken);

        return filePath;
    }

    /// <summary>
    /// Loads the long project description shown on the details page.
    /// </summary>
    public async Task<string> GetProjectDescriptionAsync(
        ModSearchResult project,
        string? curseForgeApiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);

        try
        {
            if (string.Equals(project.Source, "CurseForge", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(curseForgeApiKey))
                {
                    return project.Description;
                }

                var url = $"{CurseForgeBaseUrl}/mods/{Uri.EscapeDataString(project.ProjectId)}/description";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("x-api-key", curseForgeApiKey);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                using var response = await HttpClient.SendAsync(request, cancellationToken);
                await EnsureSuccessfulAsync(response, "CurseForge", cancellationToken);

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var result = await JsonSerializer.DeserializeAsync<CurseForgeStringResponse>(
                    stream, JsonOptions, cancellationToken);
                return StripHtml(result?.Data) ?? project.Description;
            }

            var modrinthUrl = $"{ModrinthBaseUrl}/project/{Uri.EscapeDataString(project.ProjectId)}";
            using var modrinthResponse = await HttpClient.GetAsync(
                modrinthUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await EnsureSuccessfulAsync(modrinthResponse, "Modrinth", cancellationToken);

            await using var modrinthStream = await modrinthResponse.Content.ReadAsStreamAsync(cancellationToken);
            var modrinthProject = await JsonSerializer.DeserializeAsync<ModrinthProjectDto>(
                modrinthStream, JsonOptions, cancellationToken);

            return string.IsNullOrWhiteSpace(modrinthProject?.Body)
                ? modrinthProject?.Description ?? project.Description
                : modrinthProject.Body;
        }
        catch
        {
            return project.Description;
        }
    }

    private static async Task<IReadOnlyList<ModSearchResult>> SearchModrinthAsync(
        string query,
        string projectType,
        CancellationToken cancellationToken)
    {
        var facets = Uri.EscapeDataString($"[[\"project_type:{NormalizeProjectType(projectType)}\"]]");
        var url = $"{ModrinthBaseUrl}/search" +
                  $"?query={Uri.EscapeDataString(query)}" +
                  "&limit=20&index=relevance" +
                  $"&facets={facets}";

        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessfulAsync(response, "Modrinth", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<ModrinthSearchResponse>(stream, JsonOptions, cancellationToken);
        if (result?.Hits is null)
        {
            return [];
        }

        return result.Hits
            .Select(MapModrinthHit)
            .Where(hit => !string.IsNullOrWhiteSpace(hit.ProjectId))
            .ToList();
    }

    private static ModSearchResult MapModrinthHit(ModrinthSearchHitDto hit)
    {
        return new ModSearchResult(
            "Modrinth",
            hit.ProjectId ?? string.Empty,
            hit.Slug,
            hit.Title ?? "未命名项目",
            hit.Author ?? "未知作者",
            hit.Description ?? string.Empty,
            NormalizeProjectType(hit.ProjectType),
            hit.Downloads,
            hit.Categories ?? [],
            hit.IconUrl,
            BuildModrinthProjectUrl(hit.ProjectType, hit.Slug, hit.ProjectId));
    }

    private static async Task<IReadOnlyList<ModSearchResult>> SearchCurseForgeAsync(
        string query,
        string projectType,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "CurseForge 搜索需要 API Key。请点击“配置 CurseForge Key”填入密钥，" +
                "或设置 LITMC_CURSEFORGE_API_KEY 环境变量。");
        }

        var classId = IsModpack(projectType) ? CurseForgeModpackClassId : CurseForgeModClassId;
        var url = $"{CurseForgeBaseUrl}/mods/search" +
                  $"?gameId={CurseForgeMinecraftGameId}" +
                  $"&classId={classId}" +
                  $"&searchFilter={Uri.EscapeDataString(query)}" +
                  "&sortField=2&sortOrder=desc&index=0&pageSize=20";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessfulAsync(response, "CurseForge", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<CurseForgeResponse<CurseForgeModDto>>(
            stream, JsonOptions, cancellationToken);

        if (result?.Data is null)
        {
            return [];
        }

        return result.Data
            .Select(mod => new ModSearchResult(
                "CurseForge",
                mod.Id.ToString(),
                mod.Slug,
                mod.Name ?? "未命名项目",
                mod.Authors?.FirstOrDefault()?.Name ?? "未知作者",
                mod.Summary ?? string.Empty,
                mod.ClassId == CurseForgeModpackClassId ? "modpack" : "mod",
                mod.DownloadCount,
                mod.Categories?.Select(category => category.Name ?? string.Empty)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList() ?? [],
                mod.Logo?.ThumbnailUrl ?? mod.Logo?.Url,
                BuildCurseForgeProjectUrl(mod.ClassId, mod.Slug, mod.Id)))
            .ToList();
    }

    private static async Task<IReadOnlyList<ModVersionInfo>> GetModrinthVersionsAsync(
        ModSearchResult project,
        CancellationToken cancellationToken)
    {
        var url = $"{ModrinthBaseUrl}/project/{Uri.EscapeDataString(project.ProjectId)}/version";
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessfulAsync(response, "Modrinth", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var versions = await JsonSerializer.DeserializeAsync<List<ModrinthVersionDto>>(
            stream, JsonOptions, cancellationToken);

        if (versions is null)
        {
            return [];
        }

        return versions
            .Where(version => version.Files is { Count: > 0 })
            .Select(version =>
            {
                var file = version.Files!.FirstOrDefault(item => item.Primary) ?? version.Files![0];
                return new ModVersionInfo(
                    version.Id ?? string.Empty,
                    version.Name ?? string.Empty,
                    version.VersionNumber ?? version.Name ?? "未知版本",
                    version.DatePublished,
                    version.GameVersions ?? [],
                    version.Loaders ?? [],
                    file.Filename,
                    file.Url,
                    file.Size);
            })
            .OrderByDescending(version => version.PublishedAt)
            .ToList();
    }

    private static async Task<IReadOnlyList<ModVersionInfo>> GetCurseForgeVersionsAsync(
        ModSearchResult project,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("CurseForge 需要 API Key 才能读取版本列表。");
        }

        var files = new List<CurseForgeFileDto>();

        for (var index = 0; index < 200; index += 50)
        {
            var page = await RequestCurseForgeFilesAsync(
                project.ProjectId,
                $"gameId={CurseForgeMinecraftGameId}&index={index}&pageSize=50",
                apiKey,
                cancellationToken);

            files.AddRange(page);

            if (page.Count < 50)
            {
                break;
            }
        }

        return files
            .Where(file => file.IsAvailable != false)
            .OrderByDescending(file => file.FileDate)
            .Select(file => new ModVersionInfo(
                file.Id.ToString(),
                file.DisplayName ?? file.FileName ?? "未知版本",
                file.DisplayName ?? file.FileName ?? "未知版本",
                file.FileDate ?? default,
                file.GameVersions ?? [],
                [],
                file.FileName,
                file.DownloadUrl,
                file.FileLength))
            .ToList();
    }

    private static string BuildModrinthProjectUrl(string? projectType, string? slug, string? projectId)
    {
        var section = IsModpack(projectType) ? "modpack" : "mod";
        return $"https://modrinth.com/{section}/{slug ?? projectId}";
    }

    private static string BuildCurseForgeProjectUrl(int classId, string? slug, long projectId)
    {
        var section = classId == CurseForgeModpackClassId ? "modpacks" : "mc-mods";
        return $"https://www.curseforge.com/minecraft/{section}/{slug ?? projectId.ToString()}";
    }

    private static string? StripHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static async Task<ModInstallResult> InstallModrinthAsync(
        ModSearchResult project,
        MinecraftInstance instance,
        string targetDirectory,
        IProgress<ModDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var url = $"{ModrinthBaseUrl}/project/{Uri.EscapeDataString(project.ProjectId)}/version";
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessfulAsync(response, "Modrinth", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var versions = await JsonSerializer.DeserializeAsync<List<ModrinthVersionDto>>(
            stream, JsonOptions, cancellationToken);

        if (versions is null || versions.Count == 0)
        {
            throw new InvalidOperationException("该项目没有可下载的版本。");
        }

        var candidates = versions
            .Where(version => version.Files is { Count: > 0 })
            .OrderByDescending(version => version.DatePublished)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("该项目没有可下载的文件。");
        }

        var compatible = candidates.Where(version => IsCompatible(version, instance)).ToList();
        var selectedVersion = compatible.FirstOrDefault() ?? candidates[0];
        var selectedFile = selectedVersion.Files!.FirstOrDefault(file => file.Primary)
                           ?? selectedVersion.Files![0];

        if (string.IsNullOrWhiteSpace(selectedFile.Url))
        {
            throw new InvalidOperationException("Modrinth 没有返回该版本的文件下载地址。");
        }

        var fileName = GetSafeFileName(selectedFile.Filename, project.Title, project.IsModpack);
        var filePath = Path.Combine(targetDirectory, fileName);

        await DownloadFileAsync(
            selectedFile.Url,
            filePath,
            selectedFile.Size,
            progress,
            cancellationToken);

        return new ModInstallResult(
            filePath,
            fileName,
            selectedVersion.VersionNumber ?? selectedVersion.Name ?? "未知版本",
            project.IsModpack);
    }

    private static async Task<ModInstallResult> InstallCurseForgeAsync(
        ModSearchResult project,
        MinecraftInstance instance,
        string targetDirectory,
        string? apiKey,
        IProgress<ModDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("未配置 CurseForge API Key，无法下载文件。");
        }

        var files = await GetCurseForgeFilesAsync(project, instance, apiKey, cancellationToken);
        var selectedFile = ChooseCurseForgeFile(files);
        if (selectedFile is null)
        {
            throw new InvalidOperationException("没有找到可下载的 CurseForge 文件。");
        }

        var downloadUrl = selectedFile.DownloadUrl;
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            downloadUrl = await GetCurseForgeDownloadUrlAsync(
                project.ProjectId,
                selectedFile.Id,
                apiKey,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException("该 CurseForge 项目未开放第三方下载。");
        }

        var fileName = GetSafeFileName(selectedFile.FileName, project.Title, project.IsModpack);
        var filePath = Path.Combine(targetDirectory, fileName);

        await DownloadFileAsync(
            downloadUrl,
            filePath,
            selectedFile.FileLength,
            progress,
            cancellationToken);

        return new ModInstallResult(
            filePath,
            fileName,
            selectedFile.DisplayName ?? selectedFile.FileName ?? "未知版本",
            project.IsModpack);
    }

    private static async Task<List<CurseForgeFileDto>> GetCurseForgeFilesAsync(
        ModSearchResult project,
        MinecraftInstance instance,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var loaderType = GetCurseForgeModLoaderType(instance.LoaderDisplay);
        var query = $"gameId={CurseForgeMinecraftGameId}&pageSize=50";
        if (!string.IsNullOrWhiteSpace(instance.GameVersion))
        {
            query += $"&gameVersion={Uri.EscapeDataString(instance.GameVersion)}";
        }

        if (loaderType is not null)
        {
            query += $"&modLoaderType={loaderType}";
        }

        var files = await RequestCurseForgeFilesAsync(project.ProjectId, query, apiKey, cancellationToken);
        if (files.Count > 0)
        {
            return files;
        }

        // Fall back to the newest files when no exact loader/game version match exists.
        return await RequestCurseForgeFilesAsync(
            project.ProjectId,
            $"gameId={CurseForgeMinecraftGameId}&pageSize=50",
            apiKey,
            cancellationToken);
    }

    private static async Task<List<CurseForgeFileDto>> RequestCurseForgeFilesAsync(
        string modId,
        string query,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url = $"{CurseForgeBaseUrl}/mods/{Uri.EscapeDataString(modId)}/files?{query}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessfulAsync(response, "CurseForge", cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<CurseForgeResponse<CurseForgeFileDto>>(
            stream, JsonOptions, cancellationToken);

        return result?.Data ?? [];
    }

    private static CurseForgeFileDto? ChooseCurseForgeFile(IEnumerable<CurseForgeFileDto> files)
    {
        return files
            .Where(file => file.IsAvailable != false)
            .OrderByDescending(file => file.ReleaseType == 1)
            .ThenByDescending(file => file.FileDate)
            .FirstOrDefault();
    }

    private static async Task<string?> GetCurseForgeDownloadUrlAsync(
        string modId,
        long fileId,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var url = $"{CurseForgeBaseUrl}/mods/{Uri.EscapeDataString(modId)}/files/{fileId}/download-url";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<CurseForgeStringResponse>(
            stream, JsonOptions, cancellationToken);
        return result?.Data;
    }

    private static bool IsCompatible(ModrinthVersionDto version, MinecraftInstance instance)
    {
        var gameVersionMatches = version.GameVersions?.Contains(instance.GameVersion, StringComparer.OrdinalIgnoreCase) == true;
        if (!gameVersionMatches)
        {
            return false;
        }

        var loader = GetModrinthLoader(instance.LoaderDisplay);
        return loader is null ||
               version.Loaders?.Contains(loader, StringComparer.OrdinalIgnoreCase) == true;
    }

    private static string? GetModrinthLoader(string loaderDisplay)
    {
        return loaderDisplay switch
        {
            "Fabric" => "fabric",
            "Forge" => "forge",
            "NeoForge" => "neoforge",
            "Quilt" => "quilt",
            _ => null,
        };
    }

    private static int? GetCurseForgeModLoaderType(string loaderDisplay)
    {
        return loaderDisplay switch
        {
            "Forge" => 1,
            "Fabric" => 4,
            "Quilt" => 5,
            "NeoForge" => 6,
            _ => null,
        };
    }

    private static async Task DownloadFileAsync(
        string url,
        string destinationPath,
        long expectedSize,
        IProgress<ModDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessfulAsync(response, "文件下载", cancellationToken);

        var totalBytes = response.Content.Headers.ContentLength ?? expectedSize;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long downloadedBytes = 0;
        var lastPercentage = -1d;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            downloadedBytes += read;

            if (totalBytes <= 0)
            {
                continue;
            }

            var percentage = downloadedBytes * 100d / totalBytes;
            if (percentage - lastPercentage < 0.5 && percentage < 100)
            {
                continue;
            }

            lastPercentage = percentage;
            progress?.Report(new ModDownloadProgress(
                percentage,
                $"正在下载 {Path.GetFileName(destinationPath)} " +
                $"{downloadedBytes / 1024d / 1024d:F1} MB / {totalBytes / 1024d / 1024d:F1} MB"));
        }

        progress?.Report(new ModDownloadProgress(100, $"下载完成：{Path.GetFileName(destinationPath)}"));
    }

    private static async Task EnsureSuccessfulAsync(
        HttpResponseMessage response,
        string serviceName,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = string.Empty;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new InvalidOperationException($"{serviceName} 拒绝了请求（403），请检查 API Key 是否有效。");
        }

        if (body.Length > 260)
        {
            body = body[..260];
        }

        throw new InvalidOperationException(
            $"{serviceName} 请求失败（{(int)response.StatusCode}）：{body}");
    }

    private static string NormalizeProjectType(string? projectType)
    {
        return IsModpack(projectType) ? "modpack" : "mod";
    }

    private static bool IsModpack(string? projectType)
    {
        return string.Equals(projectType, "modpack", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSafeFileName(string? fileName, string projectTitle, bool isModpack)
    {
        var candidate = string.IsNullOrWhiteSpace(fileName)
            ? $"{projectTitle}{(isModpack ? ".mrpack" : ".jar")}"
            : Path.GetFileName(fileName);

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(candidate
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = $"download{(isModpack ? ".mrpack" : ".jar")}";
        }

        return sanitized;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("LiteMCLauncher/0.1.0 (Minecraft launcher)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    private sealed class ModrinthProjectDto
    {
        public string? Body { get; set; }

        public string? Description { get; set; }

        public List<string>? Gallery { get; set; }
    }

    private sealed class ModrinthSearchResponse
    {
        [JsonPropertyName("hits")]
        public List<ModrinthSearchHitDto>? Hits { get; set; }

        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        [JsonPropertyName("total_hits")]
        public int TotalHits { get; set; }
    }

    private sealed class ModrinthSearchHitDto
    {
        [JsonPropertyName("project_id")]
        public string? ProjectId { get; set; }

        [JsonPropertyName("project_type")]
        public string? ProjectType { get; set; }

        public string? Slug { get; set; }

        public string? Title { get; set; }

        public string? Author { get; set; }

        public string? Description { get; set; }

        public long Downloads { get; set; }

        [JsonPropertyName("icon_url")]
        public string? IconUrl { get; set; }

        public List<string>? Categories { get; set; }
    }

    private sealed class ModrinthVersionDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("version_number")]
        public string? VersionNumber { get; set; }

        public string? Name { get; set; }

        [JsonPropertyName("game_versions")]
        public List<string>? GameVersions { get; set; }

        public List<string>? Loaders { get; set; }

        [JsonPropertyName("date_published")]
        public DateTimeOffset DatePublished { get; set; }

        public List<ModrinthFileDto>? Files { get; set; }
    }

    private sealed class ModrinthFileDto
    {
        public string? Url { get; set; }

        public string? Filename { get; set; }

        public bool Primary { get; set; }

        public long Size { get; set; }
    }

    private sealed class CurseForgeResponse<T>
    {
        public List<T>? Data { get; set; }
    }

    private sealed class CurseForgeSingleResponse<T>
    {
        public T? Data { get; set; }
    }

    private sealed class CurseForgeStringResponse
    {
        public string? Data { get; set; }
    }

    private sealed class CurseForgeModDto
    {
        public long Id { get; set; }

        public string? Name { get; set; }

        public string? Slug { get; set; }

        public string? Summary { get; set; }

        public int ClassId { get; set; }

        public long DownloadCount { get; set; }

        public CurseForgeLogoDto? Logo { get; set; }

        public List<CurseForgeScreenshotDto>? Screenshots { get; set; }

        public List<CurseForgeAuthorDto>? Authors { get; set; }

        public List<CurseForgeCategoryDto>? Categories { get; set; }
    }

    private sealed class CurseForgeScreenshotDto
    {
        public string? ThumbnailUrl { get; set; }

        public string? Url { get; set; }
    }

    private sealed class CurseForgeLogoDto
    {
        public string? ThumbnailUrl { get; set; }

        public string? Url { get; set; }
    }

    private sealed class CurseForgeAuthorDto
    {
        public string? Name { get; set; }
    }

    private sealed class CurseForgeCategoryDto
    {
        public string? Name { get; set; }
    }

    private sealed class CurseForgeFileDto
    {
        public long Id { get; set; }

        public string? FileName { get; set; }

        public string? DisplayName { get; set; }

        public string? DownloadUrl { get; set; }

        public DateTimeOffset? FileDate { get; set; }

        public int ReleaseType { get; set; }

        public long FileLength { get; set; }

        public List<string>? GameVersions { get; set; }

        public bool? IsAvailable { get; set; }
    }
}
