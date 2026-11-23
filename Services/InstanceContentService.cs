using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Enumerates the worlds, shaders, resource packs and mods of an instance.
/// </summary>
public sealed class InstanceContentService
{
    public async Task<InstanceContentSnapshot> LoadAsync(
        MinecraftInstance instance,
        LauncherState launcherState,
        CancellationToken cancellationToken = default)
    {
        var gameDirectory = launcherState.GetGameDirectory(instance);
        var modsDirectory = launcherState.GetModsDirectory(instance);

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            return new InstanceContentSnapshot(
                Enumerate(Path.Combine(gameDirectory, "saves"), directoriesOnly: true),
                Enumerate(Path.Combine(gameDirectory, "shaderpacks"), directoriesOnly: false),
                Enumerate(Path.Combine(gameDirectory, "resourcepacks"), directoriesOnly: false),
                Enumerate(modsDirectory, directoriesOnly: false));
        }, cancellationToken);
    }

    private static IReadOnlyList<InstanceContentItem> Enumerate(string directory, bool directoriesOnly)
    {
        if (!Directory.Exists(directory))
        {
            return Array.Empty<InstanceContentItem>();
        }

        var items = new List<InstanceContentItem>();

        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            try
            {
                if (directoriesOnly && !Directory.Exists(path))
                {
                    continue;
                }

                var isDirectory = Directory.Exists(path);
                var info = isDirectory
                    ? (FileSystemInfo)new DirectoryInfo(path)
                    : new FileInfo(path);

                items.Add(new InstanceContentItem(
                    Path.GetFileName(path),
                    path,
                    isDirectory,
                    isDirectory ? 0 : ((FileInfo)info).Length,
                    new DateTimeOffset(info.LastWriteTimeUtc)));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return items
            .OrderByDescending(item => item.IsDirectory)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed record InstanceContentSnapshot(
    IReadOnlyList<InstanceContentItem> Worlds,
    IReadOnlyList<InstanceContentItem> Shaders,
    IReadOnlyList<InstanceContentItem> ResourcePacks,
    IReadOnlyList<InstanceContentItem> Mods);
