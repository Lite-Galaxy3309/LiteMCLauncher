using System.Text.Json;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Persists optional per-instance game settings. Legacy per-instance Java
/// selections are migrated into the new format on first read.
/// </summary>
public static class InstanceSettingsStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static Dictionary<string, InstanceGameSettings>? _settings;
    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "instance-settings.json");

    public static InstanceGameSettings Get(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return new InstanceGameSettings();
        }

        lock (Gate)
        {
            var settings = Load();
            if (settings.TryGetValue(instanceId, out var current))
            {
                return current.Clone();
            }

            var legacyJava = InstanceJavaStore.GetPath(instanceId);
            var migrated = new InstanceGameSettings
            {
                Enabled = !string.IsNullOrWhiteSpace(legacyJava),
                JavaPath = legacyJava,
            };

            if (migrated.Enabled)
            {
                settings[instanceId] = migrated;
                Save(settings);
            }

            return migrated.Clone();
        }
    }

    public static void Save(string instanceId, InstanceGameSettings value)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || value is null)
        {
            return;
        }

        lock (Gate)
        {
            var settings = Load();
            settings[instanceId] = value.Clone();
            Save(settings);
        }
    }

    public static void Delete(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return;
        }

        lock (Gate)
        {
            var settings = Load();
            if (settings.Remove(instanceId))
            {
                Save(settings);
            }
        }
    }

    private static Dictionary<string, InstanceGameSettings> Load()
    {
        if (_settings is not null)
        {
            return _settings;
        }

        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _settings = JsonSerializer.Deserialize<Dictionary<string, InstanceGameSettings>>(json) ??
                            new Dictionary<string, InstanceGameSettings>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                _settings = new Dictionary<string, InstanceGameSettings>(StringComparer.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _settings = new Dictionary<string, InstanceGameSettings>(StringComparer.OrdinalIgnoreCase);
        }

        return _settings;
    }

    private static void Save(Dictionary<string, InstanceGameSettings> settings)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch
        {
            // Invalid or unwritable settings should not prevent the instance
            // from using the launcher-wide defaults.
        }
    }
}
