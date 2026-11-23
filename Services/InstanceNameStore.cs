using System.Text.Json;

namespace LiteMCLauncher.Services;

/// <summary>
/// Stores user-facing instance names separately from the version folder ID.
/// The version ID stays untouched so launch, loader and game paths keep working.
/// </summary>
public static class InstanceNameStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static Dictionary<string, string>? _names;
    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "instance-names.json");

    public static string? GetName(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        lock (Gate)
        {
            return Load().TryGetValue(instanceId, out var name) ? name : null;
        }
    }

    public static string? SetName(string instanceId, string? name)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return null;
        }

        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            string.Equals(normalized, instanceId, StringComparison.Ordinal))
        {
            normalized = null;
        }
        else if (normalized.Length > 64)
        {
            normalized = normalized[..64];
        }

        lock (Gate)
        {
            var names = Load();
            if (normalized is null)
            {
                names.Remove(instanceId);
            }
            else
            {
                names[instanceId] = normalized;
            }

            Save(names);
        }

        return normalized;
    }

    private static Dictionary<string, string> Load()
    {
        if (_names is not null)
        {
            return _names;
        }

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _names = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ??
                         new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return _names;
    }

    private static void Save(Dictionary<string, string> names)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(names, JsonOptions));
        }
        catch
        {
            // The display name is a convenience; never break instance scanning.
        }
    }
}
