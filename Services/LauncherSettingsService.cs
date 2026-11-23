using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiteMCLauncher.Services;

/// <summary>
/// Small persisted launcher settings file. The CurseForge API key can also be
/// supplied with the LITMC_CURSEFORGE_API_KEY environment variable.
/// </summary>
public sealed class LauncherSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private LauncherSettings _current;

    public LauncherSettingsService()
    {
        _current = Load();
    }

    public LauncherSettings Current => _current;

    public string SettingsFilePath => Path.Combine(AppPaths.DataDirectory, "settings.json");

    public string? CurseForgeApiKey
    {
        get
        {
            var environmentKey = Environment.GetEnvironmentVariable("LITMC_CURSEFORGE_API_KEY");
            return string.IsNullOrWhiteSpace(environmentKey)
                ? _current.CurseForgeApiKey
                : environmentKey;
        }
    }

    public string? JavaPath
    {
        get => _current.JavaPath;
        set
        {
            _current.JavaPath = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            Save();
        }
    }

    public int MaximumRamMb
    {
        get => _current.MaximumRamMb > 0 ? _current.MaximumRamMb : 4096;
        set
        {
            _current.MaximumRamMb = value;
            Save();
        }
    }

    public string GameLanguage
    {
        get => string.IsNullOrWhiteSpace(_current.GameLanguage) ? "zh_cn" : _current.GameLanguage;
        set
        {
            _current.GameLanguage = value;
            Save();
        }
    }

    public string? ExtraJvmArguments
    {
        get => _current.ExtraJvmArguments;
        set
        {
            _current.ExtraJvmArguments = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            Save();
        }
    }

    public string DownloadSource
    {
        get => string.Equals(_current.DownloadSource, "BMCLAPI", StringComparison.OrdinalIgnoreCase)
            ? "BMCLAPI"
            : "Mojang";
        set
        {
            _current.DownloadSource = value;
            Save();
        }
    }

    public string Theme
    {
        get => _current.Theme is "Light" or "Dark" ? _current.Theme : "System";
        set
        {
            _current.Theme = value;
            Save();
        }
    }

    public bool MinimizeOnLaunch
    {
        get => _current.MinimizeOnLaunch;
        set
        {
            _current.MinimizeOnLaunch = value;
            Save();
        }
    }

    public string JavaDownloadSource
    {
        get => _current.JavaDownloadSource is "Adoptium" ? "Adoptium" : "TUNA";
        set
        {
            _current.JavaDownloadSource = value is "Adoptium" ? "Adoptium" : "TUNA";
            Save();
        }
    }

    public void SetCurseForgeApiKey(string? apiKey)
    {
        _current.CurseForgeApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(_current, SerializerOptions));
        }
        catch (IOException)
        {
            // Settings are best-effort; a read-only local state should not crash the UI.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                return new LauncherSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<LauncherSettings>(json, SerializerOptions)
                   ?? new LauncherSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new LauncherSettings();
        }
    }
}

public sealed class LauncherSettings
{
    public string? CurseForgeApiKey { get; set; }

    public string? JavaPath { get; set; }

    public int MaximumRamMb { get; set; } = 4096;

    public string GameLanguage { get; set; } = "zh_cn";

    public string? ExtraJvmArguments { get; set; }

    public string DownloadSource { get; set; } = "Mojang";

    public string Theme { get; set; } = "System";

    public string JavaDownloadSource { get; set; } = "TUNA";

    public bool MinimizeOnLaunch { get; set; }

    public bool EnableTransparency { get; set; } = true;

    public string LauncherLanguage { get; set; } = "zh-CN";

    public string? AccentColor { get; set; }

    public string BackgroundMode { get; set; } = "Default";

    public string? BackgroundColor { get; set; }

    public string? BackgroundImagePath { get; set; }

    public string? OfflineUsername { get; set; }
}
