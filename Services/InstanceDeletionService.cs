using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Deletes a local instance's version folder and its per-instance game data.
/// Shared libraries, assets and Java runtimes are never touched.
/// </summary>
public static class InstanceDeletionService
{
    public static async Task DeleteAsync(
        MinecraftInstance instance,
        LauncherState launcherState,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(launcherState);

        var minecraftRoot = Path.GetFullPath(AppPaths.MinecraftDirectory);
        var versionsRoot = Path.GetFullPath(AppPaths.VersionsDirectory);
        var instanceRoot = Path.GetFullPath(Path.Combine(minecraftRoot, "instances"));

        var versionDirectory = Path.GetFullPath(Path.Combine(versionsRoot, instance.Id));
        var gameDirectory = Path.GetFullPath(launcherState.GetGameDirectory(instance));

        EnsureChildPath(versionsRoot, versionDirectory, "版本目录");
        EnsureChildPath(instanceRoot, gameDirectory, "实例游戏目录");

        await Task.Run(() =>
        {
            TryDeleteDirectory(versionDirectory, cancellationToken);
            if (!string.Equals(versionDirectory, gameDirectory, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteDirectory(gameDirectory, cancellationToken);
            }
        }, cancellationToken);

        InstanceNameStore.Remove(instance.Id);
        InstanceSettingsStore.Delete(instance.Id);
        InstanceJavaStore.Remove(instance.Id);

        if (launcherState.SelectedInstance is { } selected &&
            string.Equals(selected.Id, instance.Id, StringComparison.Ordinal))
        {
            launcherState.SelectedInstance = null;
        }
    }

    private static void EnsureChildPath(string root, string path, string label)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{label}不在预期的 Minecraft 目录内，已取消删除。");
        }
    }

    private static void TryDeleteDirectory(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"无法删除 {path}：{exception.Message}", exception);
        }
    }
}
