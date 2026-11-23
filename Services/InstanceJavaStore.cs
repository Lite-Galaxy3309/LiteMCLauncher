using System.Text.Json;

namespace LiteMCLauncher.Services;

/// <summary>
/// Stores an optional per-instance Java executable path. When absent, the
/// launcher-wide default Java setting is used.
/// </summary>
public static class InstanceJavaStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static Dictionary<string, string>? _paths;
    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "instance-java.json");

    public static string? GetPath(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        lock (Gate)
        {
            return Load().TryGetValue(instanceId, out var path) ? path : null;
        }
    }

    public static string? SetPath(string instanceId, string? path)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        var normalized = path?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = null;
        }

        lock (Gate)
        {
            var paths = Load();
            if (normalized is null)
            {
                paths.Remove(instanceId);
            }
            else
            {
                paths[instanceId] = normalized;
            }

            Save(paths);
        }

        return normalized;
    }

    private static Dictionary<string, string> Load()
    {
        if (_paths is not null)
        {
            return _paths;
        }

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _paths = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
                         new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return _paths;
    }

    private static void Save(Dictionary<string, string> paths)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(paths, JsonOptions));
        }
        catch
        {
            // Java selection should never break instance management.
        }
    }
}
