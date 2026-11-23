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
    private List<MinecraftVersionInfo> _allVersions = [];
    private bool _isLoading;
    private string? _errorMessage;
    private string _searchText = string.Empty;
    private string _typeFilter = "全部版本";
    private string? _loaderIconPath;

    public DownloadsViewModel(MinecraftInstallService installService)
    {
        _installService = installService;
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
        ? "正在连接 Mojang..."
        : _errorMessage is not null
            ? "版本列表读取失败"
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
                ? "尝试调整搜索关键词或版本类型。"
                : "请检查网络连接后点击“刷新列表”。";
        }
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
        _typeFilter = string.IsNullOrWhiteSpace(value) ? "全部版本" : value;
        ApplyFilter();
    }

    public void SetLoader(string? loaderName)
    {
        _loaderIconPath = loaderName switch
        {
            "Fabric" => "ms-appx:///Assets/Loaders/fabric.png",
            "Forge" => "ms-appx:///Assets/Loaders/forge.png",
            "NeoForge" => "ms-appx:///Assets/Loaders/neoforge.png",
            "Quilt" => "ms-appx:///Assets/Loaders/quilt.png",
            _ => null,
        };

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
