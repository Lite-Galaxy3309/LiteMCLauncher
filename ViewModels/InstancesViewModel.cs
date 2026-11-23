using System.Collections.ObjectModel;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;

namespace LiteMCLauncher.ViewModels;

/// <summary>
/// Loads and filters the local instances shown on the Instances page.
/// </summary>
public sealed class InstancesViewModel : ObservableObject
{
    private readonly MinecraftInstanceService _instanceService;
    private readonly LauncherState _launcherState;
    private List<MinecraftInstance> _allInstances = [];
    private MinecraftInstance? _selectedInstance;
    private bool _isLoading;
    private string? _errorMessage;
    private string _searchText = string.Empty;
    private string _loaderFilter = "全部加载器";
    private string _sortMode = "最近修改";

    public InstancesViewModel(MinecraftInstanceService instanceService, LauncherState launcherState)
    {
        _instanceService = instanceService;
        _launcherState = launcherState;
    }

    public ObservableCollection<MinecraftInstance> Instances { get; } = [];

    public string MinecraftDirectory => _instanceService.MinecraftDirectory;

    public string VersionsDirectory => _instanceService.VersionsDirectory;

    public MinecraftInstance? SelectedInstance
    {
        get => _selectedInstance;
        set
        {
            if (SetProperty(ref _selectedInstance, value))
            {
                _launcherState.SelectedInstance = value;
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(InstanceCountText));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsEmpty => !IsLoading && Instances.Count == 0;

    public string InstanceCountText => IsLoading
        ? "正在读取实例…"
        : $"共 {Instances.Count} 个实例";

    public string StatusText => IsLoading
        ? "正在扫描 .minecraft/versions"
        : _errorMessage is not null
            ? "扫描失败"
            : $"{Instances.Count} 个结果";

    public string EmptyStateTitle
    {
        get
        {
            if (_errorMessage is not null)
            {
                return "读取实例失败";
            }

            return _allInstances.Count > 0 ? "没有匹配的实例" : "未找到本地实例";
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

            if (_allInstances.Count > 0)
            {
                return "尝试调整搜索关键词或筛选条件。";
            }

            return $"请先安装 Minecraft，或确认以下目录存在：{VersionsDirectory}";
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
            _allInstances = (await _instanceService.GetInstancesAsync()).ToList();
        }
        catch (Exception exception)
        {
            _allInstances = [];
            _errorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
            ApplyFilter();
        }
    }

    public void SetSearchText(string? value)
    {
        _searchText = value?.Trim() ?? string.Empty;
        ApplyFilter();
    }

    public void SetLoaderFilter(string? value)
    {
        _loaderFilter = string.IsNullOrWhiteSpace(value) ? "全部加载器" : value;
        ApplyFilter();
    }

    public void SetSortMode(string? value)
    {
        _sortMode = string.IsNullOrWhiteSpace(value) ? "最近修改" : value;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var preferredId = _selectedInstance?.Id ?? _launcherState.SelectedInstance?.Id;
        IEnumerable<MinecraftInstance> query = _allInstances;

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            query = query.Where(instance =>
                instance.Id.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                instance.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                instance.GameVersion.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                instance.LoaderDisplay.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(_loaderFilter, "全部加载器", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(instance =>
                string.Equals(instance.LoaderDisplay, _loaderFilter, StringComparison.OrdinalIgnoreCase));
        }

        query = _sortMode switch
        {
            "名称" => query.OrderBy(instance => instance.DisplayName, StringComparer.OrdinalIgnoreCase),
            "版本" => query
                .OrderByDescending(instance => instance.GameVersion, StringComparer.OrdinalIgnoreCase)
                .ThenBy(instance => instance.Id, StringComparer.OrdinalIgnoreCase),
            _ => query
                .OrderByDescending(instance => instance.LastWriteTime)
                .ThenBy(instance => instance.Id, StringComparer.OrdinalIgnoreCase),
        };

        Instances.Clear();
        foreach (var instance in query)
        {
            Instances.Add(instance);
        }

        var selected = Instances.FirstOrDefault(instance => instance.Id == preferredId)
                       ?? Instances.FirstOrDefault();

        if (!Equals(_selectedInstance, selected))
        {
            _selectedInstance = selected;
            OnPropertyChanged(nameof(SelectedInstance));
        }

        _launcherState.SelectedInstance = selected;

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(InstanceCountText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
    }
}
