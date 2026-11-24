using System.Text.Json;
using System.Text.RegularExpressions;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Resolves which Minecraft versions actually have builds for a mod loader.
/// The launcher only shows/offers loader installs for versions returned here.
/// </summary>
public sealed class ModLoaderAvailabilityService
{
    private const string FabricGamesUrl = "https://meta.fabricmc.net/v2/versions/game";
    private const string FabricLoaderUrl = "https://meta.fabricmc.net/v2/versions/loader";
    private const string ForgePromotionsUrl =
        "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";
    private const string ForgeMetadataUrl =
        "https://files.minecraftforge.net/net/minecraftforge/forge/maven-metadata.json";
    private const string NeoForgeVersionsUrl =
        "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge";

    private const string BmclBase = "https://bmclapi2.bangbang93.com";
    private const string BmclFabricGamesUrl = BmclBase + "/fabric-meta/v2/versions/game";
    private const string BmclFabricLoaderUrl = BmclBase + "/fabric-meta/v2/versions/loader";
    private const string BmclForgeMetadataUrl = BmclBase + "/maven/net/minecraftforge/forge/maven-metadata.json";
    private const string BmclNeoForgeVersionsUrl =
        BmclBase + "/neoforge/meta/api/maven/details/releases/net/neoforged/neoforge";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private static readonly VersionTextComparer VersionComparer = new();

    private static readonly Dictionary<string, IReadOnlySet<string>> Cache = [];
    private static readonly SemaphoreSlim CacheGate = new(1, 1);
    private static Dictionary<string, List<string>>? _officialForgeMetadata;
    private static Dictionary<string, List<string>>? _bmclForgeMetadata;
    private static List<string>? _officialNeoForgeVersions;
    private static List<string>? _bmclNeoForgeVersions;

    private static bool UseBmclApi =>
        string.Equals(AppServices.Settings.DownloadSource, "BMCLAPI", StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlySet<string>> GetSupportedGameVersionsAsync(
        ModLoaderType loader,
        CancellationToken cancellationToken = default)
    {
        if (loader == ModLoaderType.Vanilla)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        await CacheGate.WaitAsync(cancellationToken);
        try
        {
            var cacheKey = $"{(UseBmclApi ? "BMCLAPI" : "Official")}:{loader}";
            if (Cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var versions = loader switch
            {
                ModLoaderType.Fabric => await GetMetaGameVersionsAsync(
                    UseBmclApi ? BmclFabricGamesUrl : FabricGamesUrl, cancellationToken),
                ModLoaderType.Forge => await GetForgeGameVersionsAsync(cancellationToken),
                ModLoaderType.NeoForge => await GetNeoForgeGameVersionsAsync(cancellationToken),
                _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            };

            Cache[cacheKey] = versions;
            return versions;
        }
        finally
        {
            CacheGate.Release();
        }
    }

    public async Task<IReadOnlyList<LoaderVersionOption>> GetLoaderVersionsAsync(
        string gameVersion,
        ModLoaderType loader,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gameVersion) || loader == ModLoaderType.Vanilla)
        {
            return [];
        }

        return loader switch
        {
            ModLoaderType.Fabric => await GetFabricLoaderVersionsAsync(gameVersion, cancellationToken),
            ModLoaderType.Forge => await GetForgeLoaderVersionsAsync(gameVersion, cancellationToken),
            ModLoaderType.NeoForge => await GetNeoForgeLoaderVersionsAsync(gameVersion, cancellationToken),
            _ => [],
        };
    }

    private static async Task<IReadOnlyList<LoaderVersionOption>> GetFabricLoaderVersionsAsync(
        string gameVersion,
        CancellationToken cancellationToken)
    {
        var response = await HttpClient.GetAsync(
            $"{(UseBmclApi ? BmclFabricLoaderUrl : FabricLoaderUrl)}/{Uri.EscapeDataString(gameVersion)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var entries = await JsonSerializer.DeserializeAsync<List<FabricLoaderEntry>>(
            stream, JsonOptions, cancellationToken);

        var loaders = entries?
            .Select(entry => entry.Loader)
            .Where(loader => !string.IsNullOrWhiteSpace(loader?.Version))
            .OrderByDescending(loader => loader!.Version, VersionComparer)
            .ToList() ?? [];

        var stable = loaders.Where(loader => loader!.Stable).ToList();
        var candidates = stable.Count > 0 ? stable : loaders;
        var versions = LimitToLatestVersionLine(
            candidates.Select(loader => loader!.Version!).ToList());
        return versions
            .Select(version => new LoaderVersionOption(
                $"Fabric Loader {version}",
                version,
                true))
            .ToList();
    }

    private static async Task<IReadOnlyList<LoaderVersionOption>> GetForgeLoaderVersionsAsync(
        string gameVersion,
        CancellationToken cancellationToken)
    {
        var metadata = await GetForgeMetadataAsync(cancellationToken);
        if (metadata is null ||
            !metadata.TryGetValue(gameVersion, out var builds))
        {
            return [];
        }

        var prefix = gameVersion + "-";
        var versions = LimitToLatestVersionLine(
            builds
                .Where(build => build.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(build => build[prefix.Length..])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList());

        return versions
            .Select(version => new LoaderVersionOption($"Forge {version}", version))
            .ToList();
    }

    private static async Task<IReadOnlyList<LoaderVersionOption>> GetNeoForgeLoaderVersionsAsync(
        string gameVersion,
        CancellationToken cancellationToken)
    {
        var allVersions = await GetNeoForgeVersionsAsync(cancellationToken);
        var versions = LimitToLatestVersionLine(
            allVersions
                .Where(version => string.Equals(
                    MapNeoForgeGameVersion(version), gameVersion, StringComparison.OrdinalIgnoreCase))
                .ToList());

        return versions
            .Select(version => new LoaderVersionOption($"NeoForge {version}", version))
            .ToList();
    }

    private static async Task<IReadOnlySet<string>> GetMetaGameVersionsAsync(
        string url,
        CancellationToken cancellationToken)
    {
        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var entries = await JsonSerializer.DeserializeAsync<List<MetaGameVersion>>(
            stream, JsonOptions, cancellationToken);

        return new HashSet<string>(
            entries?
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Version))
                .Select(entry => entry.Version!) ?? [],
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlySet<string>> GetForgeGameVersionsAsync(
        CancellationToken cancellationToken)
    {
        var metadata = await GetForgeMetadataAsync(cancellationToken);
        if (metadata is { Count: > 0 })
        {
            return new HashSet<string>(
                metadata.Keys.Where(IsGameVersionKey),
                StringComparer.OrdinalIgnoreCase);
        }

        if (UseBmclApi)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var promotionsResponse = await HttpClient.GetAsync(ForgePromotionsUrl, cancellationToken);
        promotionsResponse.EnsureSuccessStatusCode();

        await using var promotionsStream = await promotionsResponse.Content.ReadAsStreamAsync(cancellationToken);
        var feed = await JsonSerializer.DeserializeAsync<ForgePromotionsFeed>(
            promotionsStream, JsonOptions, cancellationToken);

        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (feed?.Promos is not null)
        {
            foreach (var key in feed.Promos.Keys)
            {
                var separator = key.LastIndexOf("-recommended", StringComparison.OrdinalIgnoreCase);
                if (separator < 0)
                {
                    separator = key.LastIndexOf("-latest", StringComparison.OrdinalIgnoreCase);
                }

                if (separator > 0)
                {
                    versions.Add(key[..separator]);
                }
            }
        }

        return versions;
    }

    private static async Task<Dictionary<string, List<string>>?> GetForgeMetadataAsync(
        CancellationToken cancellationToken)
    {
        var useBmcl = UseBmclApi;
        if (useBmcl && _bmclForgeMetadata is not null)
        {
            return _bmclForgeMetadata;
        }

        if (!useBmcl && _officialForgeMetadata is not null)
        {
            return _officialForgeMetadata;
        }

        try
        {
            var url = useBmcl ? BmclForgeMetadataUrl : ForgeMetadataUrl;
            var response = await HttpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var metadata = await JsonSerializer.DeserializeAsync<Dictionary<string, List<string>>>(
                stream, JsonOptions, cancellationToken);

            if (useBmcl)
            {
                _bmclForgeMetadata = metadata;
            }
            else
            {
                _officialForgeMetadata = metadata;
            }

            return metadata;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            if (useBmcl)
            {
                _bmclForgeMetadata = null;
            }
            else
            {
                _officialForgeMetadata = null;
            }

            return null;
        }
    }

    private static bool IsGameVersionKey(string key)
    {
        return !key.Contains('_', StringComparison.Ordinal) &&
               Regex.IsMatch(key, @"^\d+\.\d+(?:\.\d+)?$");
    }

    private static async Task<IReadOnlySet<string>> GetNeoForgeGameVersionsAsync(
        CancellationToken cancellationToken)
    {
        var allVersions = await GetNeoForgeVersionsAsync(cancellationToken);
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var version in allVersions)
        {
            var gameVersion = MapNeoForgeGameVersion(version);
            if (!string.IsNullOrWhiteSpace(gameVersion))
            {
                versions.Add(gameVersion);
            }
        }

        return versions;
    }

    private static async Task<List<string>> GetNeoForgeVersionsAsync(
        CancellationToken cancellationToken)
    {
        if (UseBmclApi)
        {
            if (_bmclNeoForgeVersions is not null)
            {
                return _bmclNeoForgeVersions;
            }

            var bmclResponse = await HttpClient.GetAsync(BmclNeoForgeVersionsUrl, cancellationToken);
            bmclResponse.EnsureSuccessStatusCode();

            await using var bmclStream = await bmclResponse.Content.ReadAsStreamAsync(cancellationToken);
            var bmclFeed = await JsonSerializer.DeserializeAsync<NeoForgeBmclFeed>(
                bmclStream, JsonOptions, cancellationToken);
            _bmclNeoForgeVersions = bmclFeed?.Files?
                .Where(file => string.Equals(file.Type, "DIRECTORY", StringComparison.OrdinalIgnoreCase))
                .Select(file => file.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList() ?? [];
            return _bmclNeoForgeVersions;
        }

        if (_officialNeoForgeVersions is not null)
        {
            return _officialNeoForgeVersions;
        }

        var officialResponse = await HttpClient.GetAsync(NeoForgeVersionsUrl, cancellationToken);
        officialResponse.EnsureSuccessStatusCode();
        await using var officialStream = await officialResponse.Content.ReadAsStreamAsync(cancellationToken);
        var officialFeed = await JsonSerializer.DeserializeAsync<NeoForgeVersionFeed>(
            officialStream, JsonOptions, cancellationToken);
        _officialNeoForgeVersions = officialFeed?.Versions ?? [];
        return _officialNeoForgeVersions;
    }

    private static List<string> LimitToLatestVersionLine(
        IReadOnlyList<string> versions,
        int maxCount = 5)
    {
        if (versions.Count == 0)
        {
            return [];
        }

        var ordered = versions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(version => version, VersionComparer)
            .ToList();
        var latest = ordered[0];
        var parts = latest.Split('.');
        var line = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : latest;

        return ordered
            .Where(version =>
                string.Equals(version, line, StringComparison.OrdinalIgnoreCase) ||
                version.StartsWith(line + ".", StringComparison.OrdinalIgnoreCase))
            .Take(maxCount)
            .ToList();
    }

    private static string? MapNeoForgeGameVersion(string neoVersion)
    {
        // NeoForge uses versions such as:
        //   1.20.4 -> 20.4.237, 1.21.1 -> 21.1.209,
        //   26.1.2 -> 26.1.2.71, 26.2 -> 26.2.0.57.
        if (neoVersion.StartsWith("0.", StringComparison.Ordinal) ||
            neoVersion.Contains("snapshot", StringComparison.OrdinalIgnoreCase) ||
            neoVersion.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
            neoVersion.Contains("beta", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var match = Regex.Match(neoVersion, @"^(?<a>\d+)\.(?<b>\d+)(?:\.(?<c>\d+))?");
        if (!match.Success)
        {
            return null;
        }

        var major = int.Parse(match.Groups["a"].Value);
        var minor = int.Parse(match.Groups["b"].Value);
        var patch = match.Groups["c"].Success ? int.Parse(match.Groups["c"].Value) : 0;

        if (major >= 22)
        {
            // New Minecraft versioning: 26.1.2 or 26.2 / 26.3.
            return patch > 0
                ? $"{major}.{minor}.{patch}"
                : $"{major}.{minor}";
        }

        if (major == 20 || major == 21)
        {
            return $"1.{major}.{minor}";
        }

        return null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LiteMCLauncher/0.3.0");
        return client;
    }

    private sealed class VersionTextComparer : IComparer<string?>
    {
        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var left = x.Split(['.', '-', '+'], StringSplitOptions.RemoveEmptyEntries);
            var right = y.Split(['.', '-', '+'], StringSplitOptions.RemoveEmptyEntries);
            var count = Math.Max(left.Length, right.Length);

            for (var index = 0; index < count; index++)
            {
                var leftPart = index < left.Length ? left[index] : "0";
                var rightPart = index < right.Length ? right[index] : "0";
                var leftNumber = int.TryParse(leftPart, out var leftValue);
                var rightNumber = int.TryParse(rightPart, out var rightValue);

                var comparison = leftNumber && rightNumber
                    ? leftValue.CompareTo(rightValue)
                    : string.Compare(leftPart, rightPart, StringComparison.OrdinalIgnoreCase);

                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class MetaGameVersion
    {
        public string? Version { get; set; }
    }

    private sealed class FabricLoaderEntry
    {
        public FabricLoaderInfo? Loader { get; set; }
    }

    private sealed class FabricLoaderInfo
    {
        public string? Version { get; set; }

        public bool Stable { get; set; }
    }

    private sealed class ForgePromotionsFeed
    {
        public Dictionary<string, string>? Promos { get; set; }
    }

    private sealed class NeoForgeVersionFeed
    {
        public List<string>? Versions { get; set; }
    }

    private sealed class NeoForgeBmclFeed
    {
        public List<NeoForgeBmclEntry>? Files { get; set; }
    }

    private sealed class NeoForgeBmclEntry
    {
        public string? Name { get; set; }

        public string? Type { get; set; }
    }
}
