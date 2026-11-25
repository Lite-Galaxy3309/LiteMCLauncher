using LiteMCLauncher.Models;
using SeedMap = RZeroF1.Minecraft.SeedMap;

namespace LiteMCLauncher.Services;

public static class SeedFinderService
{
    public static IReadOnlyList<SeedVersionGroup> GetVersionGroups()
    {
        var groups = new List<SeedVersionGroup>();
        var capabilities = SeedMap.Api.VersionCapabilities
            .Where(capability => VersionAtLeastOneSeven(capability.ReleaseKey))
            .Where(capability => capability.Structures == SeedMap.VersionSupportStatus.Supported)
            .ToList();

        for (var index = 0; index < capabilities.Count;)
        {
            var first = capabilities[index];
            var last = first;
            while (index + 1 < capabilities.Count &&
                   capabilities[index + 1].BiomeProfileKey == first.BiomeProfileKey &&
                   capabilities[index + 1].StructureProfileKey == first.StructureProfileKey)
            {
                index++;
                last = capabilities[index];
            }

            var display = first.ReleaseKey == last.ReleaseKey
                ? first.ReleaseKey
                : $"{first.ReleaseKey} – {last.ReleaseKey}";
            groups.Add(new SeedVersionGroup(display, last.ReleaseKey));
            index++;
        }

        groups = groups
            .OrderByDescending(group => ParseVersion(group.RepresentativeKey))
            .ToList();

        // Cubiomes 0.2.2 stops at 1.21.11. Newer game profiles are not in the
        // package, so expose 26.3 only as an explicitly approximate entry.
        var latest = groups.FirstOrDefault(group => group.RepresentativeKey == "1.21.11");
        if (latest is not null)
        {
            groups.Insert(0, new SeedVersionGroup(
                LauncherLanguageService.IsEnglish ? "26.3 (approx. 1.21.11)" : "26.3（按 1.21.11 近似）",
                latest.RepresentativeKey,
                isApproximate: true));
        }

        return groups;
    }

    public static IReadOnlyList<SeedStructureOption> GetStructures(string representativeKey)
    {
        var version = SeedMap.Api.Versions.FirstOrDefault(candidate => candidate.Key == representativeKey);
        if (version is null)
        {
            return [];
        }

        var options = SeedMap.Api.Structures(version.Id, 0)
            .Where(structure => structure.Key is not ("mineshaft" or "buried_treasure"))
            .Select(structure => new SeedStructureOption(structure.Key, DisplayName(structure.Key, structure.Name)))
            .OrderBy(option => StructureOrder(option.Key))
            .ToList();

        if (options.All(option => option.Key != "stronghold") &&
            SeedMap.Api.VersionCapabilities.Any(capability =>
                capability.ReleaseKey == representativeKey &&
                capability.Strongholds == SeedMap.VersionSupportStatus.Supported))
        {
            options.Insert(0, new SeedStructureOption("stronghold", DisplayName("stronghold", "要塞")));
        }

        return options;
    }

    private static bool VersionAtLeastOneSeven(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 &&
               int.TryParse(parts[0], out var major) &&
               int.TryParse(parts[1], out var minor) &&
               (major > 1 || (major == 1 && minor >= 7));
    }

    private static Version ParseVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length >= 2 &&
               int.TryParse(parts[0], out var major) &&
               int.TryParse(parts[1], out var minor)
            ? new Version(major, minor, parts.Length > 2 && int.TryParse(parts[2], out var patch) ? patch : 0)
            : new Version(0, 0);
    }

    private static int StructureOrder(string key) => key switch
    {
        "village" => 0,
        "stronghold" => 1,
        "monument" => 2,
        "desert_pyramid" => 3,
        "ancient_city" => 4,
        "mansion" => 5,
        _ => 100,
    };

    public static async Task<IReadOnlyList<SeedStructureResult>> SearchAsync(
        string seedText,
        string versionKey,
        string structureKey,
        int radiusChunks,
        CancellationToken cancellationToken = default)
    {
        var seed = SeedMap.Api.ParseSeed(seedText);
        var version = SeedMap.Api.Versions.FirstOrDefault(candidate => candidate.Key == versionKey)
                      ?? throw new InvalidOperationException($"Unsupported version: {versionKey}");
        var radiusBlocks = (int)Math.Min(radiusChunks * 16d, 30_000_000d);
        var world = new SeedMap.World(seed, version.Id, 0, 64);
        var bounds = new SeedMap.Bounds(-radiusBlocks, -radiusBlocks, radiusBlocks, radiusBlocks);
        var results = new List<SeedStructureResult>();

        if (structureKey == "stronghold")
        {
            using var generator = SeedMap.Api.Create(world);
            foreach (var hit in SeedMap.Api.Strongholds(generator))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddHit(results, DisplayName("stronghold", "要塞"), hit.X, hit.Z, radiusBlocks);
            }
        }
        else
        {
            var structure = SeedMap.Api.Structures(version.Id, 0)
                .FirstOrDefault(candidate => candidate.Key == structureKey)
                ?? throw new InvalidOperationException($"Unsupported structure: {structureKey}");

            using var engine = new SeedMap.Engine(world);
            var query = await engine.QueryStructuresAsync(bounds, [structure], cancellationToken);
            var name = DisplayName(structureKey, structure.Name);
            foreach (var hit in query.Hits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddHit(results, name, hit.X, hit.Z, radiusBlocks);
            }
        }

        return results.OrderBy(result => result.Distance).Take(1000).ToList();
    }

    private static void AddHit(List<SeedStructureResult> results, string name, int x, int z, int radiusBlocks)
    {
        if (Math.Abs((long)x) > radiusBlocks || Math.Abs((long)z) > radiusBlocks)
        {
            return;
        }

        var distance = Math.Sqrt(((double)x * x) + ((double)z * z));
        results.Add(new SeedStructureResult(name, x, z, distance));
    }

    private static string DisplayName(string key, string fallback) => key switch
    {
        "village" => LauncherLanguageService.IsEnglish ? "Village" : "村庄",
        "stronghold" => LauncherLanguageService.IsEnglish ? "Stronghold" : "要塞",
        "monument" => LauncherLanguageService.IsEnglish ? "Ocean Monument" : "海底神殿",
        "desert_pyramid" => LauncherLanguageService.IsEnglish ? "Desert Pyramid" : "沙漠神殿",
        "ancient_city" => LauncherLanguageService.IsEnglish ? "Ancient City" : "远古城市",
        "mansion" => LauncherLanguageService.IsEnglish ? "Woodland Mansion" : "林地府邸",
        _ => LauncherLanguageService.IsEnglish ? LauncherLanguageService.Translate(fallback) : fallback,
    };
}
