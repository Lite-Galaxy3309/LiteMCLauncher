using System.Text.Json;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Reads locally installed Minecraft versions from
/// <c>.minecraft/versions</c>.
/// </summary>
public sealed class MinecraftInstanceService
{
    public MinecraftInstanceService(string? minecraftDirectory = null)
    {
        MinecraftDirectory = string.IsNullOrWhiteSpace(minecraftDirectory)
            ? AppPaths.MinecraftDirectory
            : Path.GetFullPath(minecraftDirectory);
    }

    public string MinecraftDirectory { get; }

    public string VersionsDirectory => Path.Combine(MinecraftDirectory, "versions");

    public Task<IReadOnlyList<MinecraftInstance>> GetInstancesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<MinecraftInstance>>(
            () => LoadInstances(cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<MinecraftInstance> LoadInstances(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(VersionsDirectory))
        {
            return Array.Empty<MinecraftInstance>();
        }

        var instances = new List<MinecraftInstance>();

        foreach (var directory in Directory.EnumerateDirectories(VersionsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var instance = ReadInstance(new DirectoryInfo(directory));
                if (instance is not null)
                {
                    instances.Add(instance);
                }
            }
            catch (IOException)
            {
                // A version folder can be incomplete or temporarily locked.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore folders the current user cannot read.
            }
        }

        return instances
            .OrderByDescending(instance => instance.LastWriteTime)
            .ThenBy(instance => instance.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static MinecraftInstance? ReadInstance(DirectoryInfo directory)
    {
        var jsonPath = Path.Combine(directory.FullName, $"{directory.Name}.json");
        var jarPath = Path.Combine(directory.FullName, $"{directory.Name}.jar");
        var hasMetadata = File.Exists(jsonPath);

        if (!hasMetadata && !File.Exists(jarPath))
        {
            return null;
        }

        string? versionType = null;
        string? inheritsFrom = null;
        string? mainClass = null;
        var loaderHints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (hasMetadata)
        {
            try
            {
                using var stream = File.OpenRead(jsonPath);
                using var document = JsonDocument.Parse(stream);
                var root = document.RootElement;

                versionType = ReadString(root, "type");
                inheritsFrom = ReadString(root, "inheritsFrom");
                mainClass = ReadString(root, "mainClass");

                if (root.TryGetProperty("libraries", out var libraries) &&
                    libraries.ValueKind == JsonValueKind.Array)
                {
                    foreach (var library in libraries.EnumerateArray())
                    {
                        var libraryName = ReadString(library, "name");
                        if (string.IsNullOrWhiteSpace(libraryName))
                        {
                            continue;
                        }

                        if (libraryName.Contains("fabric-loader", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderHints.Add("Fabric");
                        }
                        else if (libraryName.Contains("quilt-loader", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderHints.Add("Quilt");
                        }
                        else if (libraryName.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderHints.Add("NeoForge");
                        }
                        else if (libraryName.Contains("forge", StringComparison.OrdinalIgnoreCase))
                        {
                            loaderHints.Add("Forge");
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Keep the folder visible even if its JSON is malformed.
            }
        }

        var loader = DetectLoader(directory.Name, inheritsFrom, mainClass, loaderHints);

        return new MinecraftInstance(
            directory.Name,
            directory.FullName,
            string.IsNullOrWhiteSpace(inheritsFrom) ? directory.Name : inheritsFrom,
            versionType,
            loader,
            new DateTimeOffset(directory.LastWriteTimeUtc),
            InstanceNameStore.GetName(directory.Name));
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string DetectLoader(
        string id,
        string? inheritsFrom,
        string? mainClass,
        HashSet<string> loaderHints)
    {
        if (loaderHints.Contains("Fabric"))
        {
            return "Fabric";
        }

        if (loaderHints.Contains("Quilt"))
        {
            return "Quilt";
        }

        if (loaderHints.Contains("NeoForge"))
        {
            return "NeoForge";
        }

        if (loaderHints.Contains("Forge"))
        {
            return "Forge";
        }

        var text = $"{id} {inheritsFrom} {mainClass}".ToLowerInvariant();

        if (text.Contains("fabric"))
        {
            return "Fabric";
        }

        if (text.Contains("quilt"))
        {
            return "Quilt";
        }

        if (text.Contains("neoforge"))
        {
            return "NeoForge";
        }

        if (text.Contains("forge"))
        {
            return "Forge";
        }

        return "原版";
    }
}
