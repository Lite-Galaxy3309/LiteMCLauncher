using System.Collections.ObjectModel;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;

namespace LiteMCLauncher.ViewModels;

/// <summary>
/// Loads the official Vanilla version manifest and filters it for Downloads.
/// </summary>
public sealed class DownloadsViewModel : ObservableObject
{
    private readonly MinecraftInstallService _installService;
    private readonly ModLoaderAvailabilityService _availabilityService;
    private List<MinecraftVersionInfo> _allVersions = [];
    private bool _isLoading;
    private string? _errorMessage;
    private string _searchText = string.Empty;
    private string _typeFilter = "全部版本";
    private string? _loaderIconPath;
    private string _loaderName = "原版";
    private IReadOnlySet<string>? _supportedGameVersions;
    private readonly HashSet<string> _unavailableGameVersions = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _loaderCancellation;

    public DownloadsViewModel(
        MinecraftInstallService installService,
        ModLoaderAvailabilityService? availabilityService = null)
    {
        _installService = installService;
        _availabilityService = availabilityService ?? AppServices.LoaderAvailability;
    }

    public ObservableCollection<MinecraftVersionInfo> Versions { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(VersionCountText));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsEmpty => !IsLoading && Versions.Count == 0;

    public string VersionCountText => IsLoading
        ? "正在读取 Mojang 版本清单…"
        : $"共 {Versions.Count} 个版本";

    public string StatusText => IsLoading
        ? $"正在检测 {_loaderName} 支持的游戏版本..."
        : _errorMessage is not null
            ? _errorMessage
            : $"{Versions.Count} 个结果";

    public string EmptyStateTitle
    {
        get
        {
            if (_errorMessage is not null)
            {
                return "读取版本清单失败";
            }

            return _allVersions.Count > 0 ? "没有匹配的版本" : "暂无可安装版本";
        }
    }

    public string EmptyStateDescription
    {
        get
        {
            if (_errorMessage is not null)
            {
                return _errorMessage;
            }

            return _allVersions.Count > 0
                ? _loaderName == "原版"
                    ? "尝试调整搜索关键词或版本类型。"
                    : $"当前筛选下没有 {_loaderName} 支持的游戏版本。Forge / NeoForge 通常不支持快照版本。"
                : "请检查网络连接后点击“刷新列表”。";
        }
    }

    public async Task SetLoaderAsync(string? loaderName)
    {
        var normalized = NormalizeLoaderName(loaderName);
        _loaderName = normalized;
        _unavailableGameVersions.Clear();
        _loaderIconPath = normalized switch
        {
            "Fabric" => "ms-appx:///Assets/Loaders/fabric.png",
            "Forge" => "ms-appx:///Assets/Loaders/forge.png",
            "NeoForge" => "ms-appx:///Assets/Loaders/neoforge.png",
            _ => null,
        };

        _loaderCancellation?.Cancel();
        _loaderCancellation?.Dispose();
        _loaderCancellation = null;

        if (normalized == "原版")
        {
            _supportedGameVersions = null;
            _errorMessage = null;
            ApplyFilter();
            return;
        }

        // Hide the previous loader's results immediately; otherwise a version
        // that has no installable build can remain visible during detection.
        _supportedGameVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ApplyFilter();

        var cancellation = new CancellationTokenSource();
        _loaderCancellation = cancellation;
        IsLoading = true;
        _errorMessage = null;
        OnPropertyChanged(nameof(StatusText));

        try
        {
            var supported = await _availabilityService.GetSupportedGameVersionsAsync(
                normalized switch
                {
                    "Fabric" => ModLoaderType.Fabric,
                    "Forge" => ModLoaderType.Forge,
                    "NeoForge" => ModLoaderType.NeoForge,
                    "Quilt" => ModLoaderType.Quilt,
                    _ => ModLoaderType.Vanilla,
                },
                cancellation.Token);

            if (!cancellation.IsCancellationRequested)
            {
                _supportedGameVersions = supported;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            _supportedGameVersions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _errorMessage = $"检测 {normalized} 支持的版本失败：{exception.Message}";
        }
        finally
        {
            if (!cancellation.IsCancellationRequested)
            {
                IsLoading = false;
                ApplyFilter();
            }
        }
    }

    public void HideVersion(string versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            return;
        }

        _unavailableGameVersions.Add(versionId);
        ApplyFilter();
    }

    private static string NormalizeLoaderName(string? loaderName)
    {
        var name = loaderName?.Trim();
        return name switch
        {
            "Fabric" => "Fabric",
            "Forge" => "Forge",
            "NeoForge" => "NeoForge",
            "Quilt" => "Quilt",
            _ => "原版",
        };
    }

    private static string NormalizeTypeFilter(string? value)
    {
        var filter = value?.Trim();
        return filter switch
        {
            "All versions" or "全部版本" => "全部版本",
            "Release" or "正式版" => "正式版",
            "Snapshot" or "快照" => "快照",
            "Legacy" or "旧版" => "旧版",
            _ => "全部版本",
        };
    }

    public async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        _errorMessage = null;
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));

        try
        {
            _allVersions = (await _installService.GetAvailableVersionsAsync()).ToList();
        }
        catch (Exception exception)
        {
            _allVersions = [];
            _errorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
            ApplyFilter();
        }
    }

    public void RefreshInstalledFlags()
    {
        _allVersions = _allVersions
            .Select(version => version with { IsInstalled = _installService.IsInstalled(version.Id) })
            .ToList();

        ApplyFilter();
    }

    public void SetSearchText(string? value)
    {
        _searchText = value?.Trim() ?? string.Empty;
        ApplyFilter();
    }

    public void SetTypeFilter(string? value)
    {
        _typeFilter = NormalizeTypeFilter(value);
        ApplyFilter();
    }

    private string GetVersionIcon(MinecraftVersionInfo version)
    {
        if (!string.IsNullOrWhiteSpace(_loaderIconPath))
        {
            return _loaderIconPath;
        }

        return version.Type switch
        {
            "snapshot" => "ms-appx:///Assets/Loaders/command_block.png",
            "old_beta" or "old_alpha" => "ms-appx:///Assets/Loaders/stone.png",
            _ => "ms-appx:///Assets/Loaders/vanilla.png",
        };
    }

    private void ApplyFilter()
    {
        IEnumerable<MinecraftVersionInfo> query = _allVersions;

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            query = query.Where(version =>
                version.Id.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        query = _typeFilter switch
        {
            "正式版" => query.Where(version => string.Equals(version.Type, "release", StringComparison.OrdinalIgnoreCase)),
            "快照" => query.Where(version => string.Equals(version.Type, "snapshot", StringComparison.OrdinalIgnoreCase)),
            "旧版" => query.Where(version =>
                string.Equals(version.Type, "old_beta", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(version.Type, "old_alpha", StringComparison.OrdinalIgnoreCase)),
            _ => query,
        };

        if (_supportedGameVersions is not null)
        {
            query = query.Where(version => _supportedGameVersions.Contains(version.Id));
        }

        query = query.Where(version => !_unavailableGameVersions.Contains(version.Id));

        Versions.Clear();
        foreach (var version in query.OrderByDescending(version => version.ReleaseTime))
        {
            version.IconPath = GetVersionIcon(version);
            Versions.Add(version);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(VersionCountText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
    }
}
