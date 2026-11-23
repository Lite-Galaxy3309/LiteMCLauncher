using System.Collections.ObjectModel;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace LiteMCLauncher.ViewModels;

/// <summary>
/// Content browser and per-instance game settings for a single instance.
/// </summary>
public sealed class InstanceManageViewModel : ObservableObject
{
    private static readonly SolidColorBrush WarnBrush = new(Color.FromArgb(255, 245, 197, 66));
    private static readonly SolidColorBrush ErrorBrush = new(Color.FromArgb(255, 255, 107, 107));
    private static readonly SolidColorBrush StackTraceBrush = new(Color.FromArgb(255, 127, 219, 255));

    private readonly InstanceContentService _contentService;
    private readonly LauncherState _launcherState;
    private readonly JavaService _javaService;
    private readonly LauncherSettingsService _settingsService;

    private MinecraftInstance? _instance;
    private InstanceGameSettings _instanceSettings = new();
    private bool _isLoading;
    private bool _loadingSettings;
    private string? _errorMessage;
    private InstanceJavaOption? _selectedJavaOption;
    private LanguageOption? _selectedLanguage;
    private string _instanceSettingsStatus = string.Empty;
    private string _logPathDisplay = "尚未找到日志";
    private string _logStatus = "还没有读取日志";

    public InstanceManageViewModel(
        InstanceContentService contentService,
        LauncherState launcherState,
        JavaService javaService)
    {
        _contentService = contentService;
        _launcherState = launcherState;
        _javaService = javaService;
        _settingsService = AppServices.Settings;
    }

    public ObservableCollection<InstanceContentItem> Worlds { get; } = [];

    public ObservableCollection<InstanceContentItem> Shaders { get; } = [];

    public ObservableCollection<InstanceContentItem> ResourcePacks { get; } = [];

    public ObservableCollection<InstanceContentItem> Mods { get; } = [];

    public ObservableCollection<InstanceJavaOption> JavaOptions { get; } = [];

    public ObservableCollection<LogLine> Logs { get; } = [];

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("简体中文", "zh_cn"),
        new("繁體中文", "zh_tw"),
        new("English (US)", "en_us"),
        new("日本語", "ja_jp"),
    ];

    public bool IsInstanceSettingsEnabled
    {
        get => _instanceSettings.Enabled;
        set
        {
            if (_loadingSettings || _instanceSettings.Enabled == value)
            {
                return;
            }

            _instanceSettings.Enabled = value;
            if (value && _instanceSettings.MaximumRamMb <= 0)
            {
                _instanceSettings.MaximumRamMb = _settingsService.MaximumRamMb;
            }

            SaveInstanceSettings();
            OnPropertyChanged();
            OnPropertyChanged(nameof(InstanceSettingsHint));
            OnPropertyChanged(nameof(JavaPathDisplay));
        }
    }

    public string InstanceSettingsHint => IsInstanceSettingsEnabled
        ? "此实例将使用本页的 Java、内存、JVM 参数和语言，不再跟随全局设置。"
        : "当前实例跟随全局游戏设置。启用后，本页参数将覆盖全局设置。";

    public double InstanceMaximumRamMb
    {
        get => _instanceSettings.MaximumRamMb;
        set
        {
            var ram = (int)Math.Clamp(value, 1024, 32768);
            if (_instanceSettings.MaximumRamMb == ram)
            {
                return;
            }

            _instanceSettings.MaximumRamMb = ram;
            OnPropertyChanged();
        }
    }

    public string InstanceJvmArguments
    {
        get => _instanceSettings.JvmArguments ?? string.Empty;
        set
        {
            if (string.Equals(_instanceSettings.JvmArguments, value, StringComparison.Ordinal))
            {
                return;
            }

            _instanceSettings.JvmArguments = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            OnPropertyChanged();
        }
    }

    public LanguageOption? SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!SetProperty(ref _selectedLanguage, value) || value is null)
            {
                return;
            }

            _instanceSettings.GameLanguage = value.Code;
        }
    }

    public InstanceJavaOption? SelectedJavaOption
    {
        get => _selectedJavaOption;
        set
        {
            if (!SetProperty(ref _selectedJavaOption, value) || _instance is null || value is null)
            {
                return;
            }

            _instanceSettings.JavaPath = value.Path;
            OnPropertyChanged(nameof(JavaPathDisplay));

            if (!_loadingSettings && _instanceSettings.Enabled)
            {
                SaveInstanceSettings();
            }
        }
    }

    public string JavaPathDisplay
    {
        get
        {
            if (!IsInstanceSettingsEnabled)
            {
                return string.IsNullOrWhiteSpace(_settingsService.JavaPath)
                    ? "全局设置：自动检测（按游戏版本选择 Java）"
                    : $"全局设置：{_settingsService.JavaPath}";
            }

            if (!string.IsNullOrWhiteSpace(_instanceSettings.JavaPath))
            {
                return _instanceSettings.JavaPath;
            }

            var required = JavaService.GetRequiredJavaMajor(_instance?.GameVersion ?? string.Empty);
            return $"实例自动检测：按游戏版本选择 Java（当前需要 Java {required}）";
        }
    }

    public string InstanceSettingsStatus
    {
        get => _instanceSettingsStatus;
        private set => SetProperty(ref _instanceSettingsStatus, value);
    }

    public string InstanceTitle => _instance?.DisplayName ?? "实例详情";

    public string InstanceSummary => _instance?.Summary ?? string.Empty;

    public string InstanceLoader => _instance?.LoaderDisplay ?? string.Empty;

    public string GameDirectory => _instance is null ? string.Empty : _launcherState.GetGameDirectory(_instance);

    public string ModsDirectory => _instance is null ? string.Empty : _launcherState.GetModsDirectory(_instance);

    public string SavesDirectory => Path.Combine(GameDirectory, "saves");

    public string ShaderpacksDirectory => Path.Combine(GameDirectory, "shaderpacks");

    public string ResourcepacksDirectory => Path.Combine(GameDirectory, "resourcepacks");

    public string LogsDirectory => Path.Combine(GameDirectory, "logs");

    public string LogPathDisplay
    {
        get => _logPathDisplay;
        private set => SetProperty(ref _logPathDisplay, value);
    }

    public string LogStatus
    {
        get => _logStatus;
        private set => SetProperty(ref _logStatus, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => IsLoading
        ? "正在读取实例内容…"
        : _errorMessage is not null
            ? _errorMessage
            : $"世界 {Worlds.Count} · 光影包 {Shaders.Count} · 资源包 {ResourcePacks.Count} · Mods {Mods.Count}";

    public async Task LoadAsync(MinecraftInstance instance)
    {
        _instance = instance;
        _errorMessage = null;
        _launcherState.SelectedInstance = instance;
        _launcherState.EnsureInstanceDirectories(instance);

        _loadingSettings = true;
        try
        {
            _instanceSettings = InstanceSettingsStore.Get(instance.Id);
            if (!_instanceSettings.Enabled)
            {
                _instanceSettings.MaximumRamMb = _settingsService.MaximumRamMb;
                _instanceSettings.JvmArguments = _settingsService.ExtraJvmArguments;
                _instanceSettings.GameLanguage = _settingsService.GameLanguage;
            }
            else if (_instanceSettings.MaximumRamMb <= 0)
            {
                _instanceSettings.MaximumRamMb = _settingsService.MaximumRamMb;
            }

            OnPropertyChanged(nameof(IsInstanceSettingsEnabled));
            OnPropertyChanged(nameof(InstanceSettingsHint));
            OnPropertyChanged(nameof(InstanceMaximumRamMb));
            OnPropertyChanged(nameof(InstanceJvmArguments));
            _selectedLanguage = LanguageOptions.FirstOrDefault(option =>
                                    string.Equals(option.Code, _instanceSettings.GameLanguage, StringComparison.OrdinalIgnoreCase))
                                ?? LanguageOptions[0];
            OnPropertyChanged(nameof(SelectedLanguage));
        }
        finally
        {
            _loadingSettings = false;
        }

        RaiseInstanceProperties();
        await RefreshAsync();
        await LoadJavaOptionsAsync();
        await RefreshLogsAsync();
    }

    public async Task RefreshJavaAsync()
    {
        await LoadJavaOptionsAsync();
    }

    public async Task SetCustomJavaPathAsync(string path)
    {
        if (_instance is null)
        {
            return;
        }

        _instanceSettings.JavaPath = path;
        if (!_instanceSettings.Enabled)
        {
            IsInstanceSettingsEnabled = true;
        }
        else
        {
            SaveInstanceSettings();
        }

        await LoadJavaOptionsAsync();
    }

    public void SaveInstanceSettings()
    {
        if (_instance is null)
        {
            return;
        }

        _instanceSettings.MaximumRamMb = (int)Math.Clamp(_instanceSettings.MaximumRamMb, 1024, 32768);
        _instanceSettings.GameLanguage = _selectedLanguage?.Code ?? _instanceSettings.GameLanguage;
        InstanceSettingsStore.Save(_instance.Id, _instanceSettings);
        InstanceSettingsStatus = IsInstanceSettingsEnabled
            ? "实例设置已保存，下次启动该实例时生效。"
            : "实例设置已关闭，将使用全局游戏设置。";
        OnPropertyChanged(nameof(JavaPathDisplay));
    }

    public async Task SaveInstanceSettingsAsync()
    {
        SaveInstanceSettings();
        await Task.CompletedTask;
    }

    private async Task LoadJavaOptionsAsync()
    {
        if (_instance is null)
        {
            return;
        }

        try
        {
            var runtimes = await _javaService.GetInstalledRuntimesAsync();
            var currentPath = _instanceSettings.Enabled
                ? _instanceSettings.JavaPath
                : _settingsService.JavaPath;

            var options = new List<InstanceJavaOption>
            {
                new("自动检测（跟随游戏版本）", null, true),
            };

            foreach (var runtime in runtimes)
            {
                options.Add(new InstanceJavaOption(
                    $"Java {runtime.Version} · {runtime.Name}",
                    runtime.PreferredExecutable));
            }

            if (!string.IsNullOrWhiteSpace(currentPath) &&
                !options.Any(option => string.Equals(option.Path, currentPath, StringComparison.OrdinalIgnoreCase)))
            {
                options.Add(new InstanceJavaOption($"自定义：{currentPath}", currentPath));
            }

            JavaOptions.Clear();
            foreach (var option in options)
            {
                JavaOptions.Add(option);
            }

            _loadingSettings = true;
            try
            {
                _selectedJavaOption = options.FirstOrDefault(option =>
                                        string.Equals(option.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                                    ?? options[0];
                OnPropertyChanged(nameof(SelectedJavaOption));
                OnPropertyChanged(nameof(JavaPathDisplay));
            }
            finally
            {
                _loadingSettings = false;
            }
        }
        catch
        {
            JavaOptions.Clear();
            JavaOptions.Add(new InstanceJavaOption("自动检测（跟随游戏版本）", null, true));
            _selectedJavaOption = JavaOptions[0];
            OnPropertyChanged(nameof(SelectedJavaOption));
            OnPropertyChanged(nameof(JavaPathDisplay));
        }
    }

    public async Task RefreshAsync()
    {
        if (_instance is null || IsLoading)
        {
            return;
        }

        IsLoading = true;

        try
        {
            var snapshot = await _contentService.LoadAsync(_instance, _launcherState);
            Replace(Worlds, snapshot.Worlds);
            Replace(Shaders, snapshot.Shaders);
            Replace(ResourcePacks, snapshot.ResourcePacks);
            Replace(Mods, snapshot.Mods);
        }
        catch (Exception exception)
        {
            _errorMessage = $"读取实例内容失败：{exception.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public async Task RefreshLogsAsync()
    {
        if (_instance is null)
        {
            return;
        }

        var logPath = FindLatestLogPath();
        LogPathDisplay = logPath ?? "尚未找到日志";
        Logs.Clear();

        if (logPath is null)
        {
            LogStatus = "尚未找到 latest.log 或其他日志文件。启动一次游戏后即可查看。";
            return;
        }

        try
        {
            var lines = await Task.Run(() => ReadLastLines(logPath, 4000));
            foreach (var line in lines)
            {
                Logs.Add(CreateLogLine(line));
            }

            LogStatus = $"{Path.GetFileName(logPath)} · 共 {Logs.Count} 行（最多显示最近 4000 行）";
        }
        catch (Exception exception)
        {
            LogStatus = $"读取日志失败：{exception.Message}";
        }
    }

    private string? FindLatestLogPath()
    {
        var directory = LogsDirectory;
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var latest = Path.Combine(directory, "latest.log");
        if (File.Exists(latest))
        {
            return latest;
        }

        try
        {
            return Directory.EnumerateFiles(directory, "*.log*")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<string> ReadLastLines(string path, int maxLines)
    {
        var buffer = new Queue<string>(maxLines);
        using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 8000)
            {
                line = line[..8000];
            }

            if (buffer.Count == maxLines)
            {
                buffer.Dequeue();
            }

            buffer.Enqueue(line);
        }

        return buffer.ToList();
    }

    private static LogLine CreateLogLine(string line)
    {
        var trimmed = line.TrimStart();

        if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Caused by:", StringComparison.OrdinalIgnoreCase))
        {
            return new LogLine { Text = line, Foreground = ErrorBrush };
        }

        if (line.Contains("WARN", StringComparison.OrdinalIgnoreCase))
        {
            return new LogLine { Text = line, Foreground = WarnBrush };
        }

        if (trimmed.StartsWith("at ", StringComparison.Ordinal) ||
            trimmed.StartsWith("at java.", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Exception", StringComparison.OrdinalIgnoreCase))
        {
            return new LogLine { Text = line, Foreground = StackTraceBrush };
        }

        return new LogLine { Text = line };
    }

    private static void Replace(
        ObservableCollection<InstanceContentItem> target,
        IReadOnlyList<InstanceContentItem> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private void RaiseInstanceProperties()
    {
        OnPropertyChanged(nameof(InstanceTitle));
        OnPropertyChanged(nameof(InstanceSummary));
        OnPropertyChanged(nameof(InstanceLoader));
        OnPropertyChanged(nameof(GameDirectory));
        OnPropertyChanged(nameof(ModsDirectory));
        OnPropertyChanged(nameof(SavesDirectory));
        OnPropertyChanged(nameof(ShaderpacksDirectory));
        OnPropertyChanged(nameof(ResourcepacksDirectory));
        OnPropertyChanged(nameof(LogsDirectory));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LogPathDisplay));
        OnPropertyChanged(nameof(LogStatus));
        OnPropertyChanged(nameof(InstanceSettingsStatus));
    }

    public sealed record LanguageOption(string Display, string Code);
}
