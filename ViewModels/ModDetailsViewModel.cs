using System.Collections.ObjectModel;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;

namespace LiteMCLauncher.ViewModels;

/// <summary>
/// Project details and all downloadable versions for ModDetailsPage.
/// Versions are grouped by loader/game version and can be filtered and expanded.
/// </summary>
public sealed class ModDetailsViewModel : ObservableObject
{
    private readonly ModPlatformService _platformService;
    private readonly LauncherState _launcherState;
    private readonly MinecraftInstanceService _instanceService;
    private readonly LauncherSettingsService _settingsService;

    private readonly List<ModVersionInfo> _allVersions = [];
    private ModSearchResult? _project;
    private string _description = string.Empty;
    private bool _isLoading;
    private string? _errorMessage;
    private string _selectedLoaderFilter = "全部加载器";
    private string _selectedGameVersionFilter = "全部版本";
    private int _filteredVersionCount;

    public ModDetailsViewModel(
        ModPlatformService platformService,
        LauncherState launcherState,
        MinecraftInstanceService instanceService,
        LauncherSettingsService settingsService)
    {
        _platformService = platformService;
        _launcherState = launcherState;
        _instanceService = instanceService;
        _settingsService = settingsService;

        LoaderFilters.Add("全部加载器");
        GameVersionFilters.Add("全部版本");
    }

    public ObservableCollection<ModVersionInfo> Versions { get; } = [];

    public ObservableCollection<ModVersionGroup> VersionGroups { get; } = [];

    public ObservableCollection<string> Gallery { get; } = [];

    public ObservableCollection<string> LoaderFilters { get; } = [];

    public ObservableCollection<string> GameVersionFilters { get; } = [];

    public bool HasGallery => Gallery.Count > 0;

    public ModVersionInfo? LatestVersion => Versions.FirstOrDefault();

    public string ProjectTitle => _project?.Title
        ?? (LauncherLanguageService.IsEnglish ? "Project details" : "项目详情");

    public string ProjectAuthor => _project?.Author ?? string.Empty;

    public string ProjectSource => _project?.Source ?? string.Empty;

    public string ProjectTypeDisplay => _project?.ProjectTypeDisplay ?? string.Empty;

    public string ProjectDownloads => _project is null ? string.Empty : $"{_project.DownloadsDisplay} 次下载";

    public string ProjectCategories => _project is null || _project.Categories.Count == 0
        ? "未标注分类"
        : string.Join(" · ", _project.Categories.Take(8));

    public string ProjectIconUrl => _project?.IconUrl ?? string.Empty;

    public Uri? ProjectUri => Uri.TryCreate(_project?.ProjectUrl, UriKind.Absolute, out var uri)
        ? uri
        : null;

    public string Description => _description;

    public string CurrentInstanceDisplay => _launcherState.SelectedInstance is { } instance
        ? $"{instance.DisplayName} · {instance.Summary}"
        : "未选择实例";

    public bool HasSelectedInstance => _launcherState.SelectedInstance is not null;

    public string SelectedLoaderFilter
    {
        get => _selectedLoaderFilter;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "全部加载器" : value;
            if (SetProperty(ref _selectedLoaderFilter, normalized))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedGameVersionFilter
    {
        get => _selectedGameVersionFilter;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "全部版本" : value;
            if (SetProperty(ref _selectedGameVersionFilter, normalized))
            {
                ApplyFilters();
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
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText => IsLoading
        ? (LauncherLanguageService.IsEnglish ? "Loading project information…" : "正在读取项目信息…")
        : _errorMessage is not null
            ? _errorMessage
            : _project is null
                ? (LauncherLanguageService.IsEnglish ? "No project data" : "没有项目数据")
                : string.Empty;

    public string VersionCountText => IsLoading
        ? (LauncherLanguageService.IsEnglish ? "Loading version list…" : "正在读取版本列表…")
        : (LauncherLanguageService.IsEnglish
            ? $"{_filteredVersionCount} versions"
            : $"共 {_filteredVersionCount} 个版本");

    public bool HasVersions => _filteredVersionCount > 0;

    public string EmptyVersionText => _allVersions.Count == 0
        ? (LauncherLanguageService.IsEnglish ? "No versions available." : "没有可用的版本。")
        : (LauncherLanguageService.IsEnglish ? "No versions match the current filters." : "没有符合当前筛选条件的版本。");

    public async Task LoadAsync(ModSearchResult project)
    {
        _project = project;
        _description = project.Description;
        _errorMessage = null;
        IsLoading = true;
        Versions.Clear();
        VersionGroups.Clear();
        _allVersions.Clear();
        Gallery.Clear();
        _filteredVersionCount = 0;
        RaiseProjectProperties();

        if (_launcherState.SelectedInstance is null)
        {
            try
            {
                _launcherState.SelectedInstance = (await _instanceService.GetInstancesAsync()).FirstOrDefault();
            }
            catch
            {
                // The details page can still show project information.
            }
        }

        var descriptionTask = _platformService.GetProjectDescriptionAsync(
            project,
            _settingsService.CurseForgeApiKey);
        var versionsTask = _platformService.GetVersionsAsync(
            project,
            _settingsService.CurseForgeApiKey);
        var galleryTask = _platformService.GetGalleryAsync(
            project,
            _settingsService.CurseForgeApiKey);

        try
        {
            var description = await descriptionTask;
            if (!string.IsNullOrWhiteSpace(description))
            {
                _description = description;
            }
        }
        catch
        {
            // Keep the short search-result description.
        }

        try
        {
            foreach (var imageUrl in await galleryTask)
            {
                Gallery.Add(imageUrl);
            }
        }
        catch
        {
            Gallery.Clear();
        }

        try
        {
            foreach (var version in await versionsTask)
            {
                _allVersions.Add(version);
                Versions.Add(version);
            }
        }
        catch (Exception exception)
        {
            _errorMessage = exception.Message;
        }
        finally
        {
            RebuildFilterOptions();
            ApplyFilters();
            IsLoading = false;
            OnPropertyChanged(nameof(HasGallery));
            RaiseProjectProperties();
        }
    }

    public async Task<string> DownloadVersionAsync(
        ModVersionInfo version,
        IProgress<ModDownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var project = _project ?? throw new InvalidOperationException("没有项目数据。");
        var instance = _launcherState.SelectedInstance
                       ?? throw new InvalidOperationException("请先在实例管理中选择一个实例。");

        var targetDirectory = project.IsModpack
            ? _launcherState.GetModpacksDirectory(instance)
            : _launcherState.GetModsDirectory(instance);

        return await _platformService.DownloadVersionAsync(
            project,
            version,
            targetDirectory,
            _settingsService.CurseForgeApiKey,
            progress,
            cancellationToken);
    }

    private void RebuildFilterOptions()
    {
        LoaderFilters.Clear();
        LoaderFilters.Add("全部加载器");

        foreach (var loader in _allVersions
                     .SelectMany(version => version.Loaders)
                     .Where(loader => !string.IsNullOrWhiteSpace(loader))
                     .Select(ToLoaderDisplay)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(LoaderSortKey)
                     .ThenBy(loader => loader, StringComparer.OrdinalIgnoreCase))
        {
            LoaderFilters.Add(loader);
        }

        GameVersionFilters.Clear();
        GameVersionFilters.Add("全部版本");

        foreach (var gameVersion in _allVersions
                     .SelectMany(version => version.GameVersions)
                     .Where(version => !string.IsNullOrWhiteSpace(version))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(GameVersionSortKey)
                     .ThenByDescending(version => version, StringComparer.OrdinalIgnoreCase))
        {
            GameVersionFilters.Add(gameVersion);
        }

        _selectedLoaderFilter = "全部加载器";
        _selectedGameVersionFilter = "全部版本";
        OnPropertyChanged(nameof(SelectedLoaderFilter));
        OnPropertyChanged(nameof(SelectedGameVersionFilter));
    }

    private void ApplyFilters()
    {
        var filteredVersions = _allVersions
            .Where(version => _selectedLoaderFilter == "全部加载器" ||
                              version.Loaders.Any(loader =>
                                  string.Equals(ToLoaderDisplay(loader), _selectedLoaderFilter, StringComparison.OrdinalIgnoreCase)))
            .Where(version => _selectedGameVersionFilter == "全部版本" ||
                              version.GameVersions.Contains(_selectedGameVersionFilter, StringComparer.OrdinalIgnoreCase))
            .ToList();

        _filteredVersionCount = filteredVersions.Count;
        VersionGroups.Clear();

        var groups = filteredVersions
            .GroupBy(version => new
            {
                Loader = GetGroupLoader(version),
                GameVersion = GetGroupGameVersion(version),
            })
            .Select(group => new ModVersionGroup(
                group.Key.Loader,
                group.Key.GameVersion,
                group.OrderByDescending(version => version.PublishedAt).ToList()))
            .OrderBy(group => LoaderSortKey(group.Loader))
            .ThenByDescending(group => GameVersionSortKey(group.GameVersion))
            .ThenBy(group => group.GameVersion, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in groups)
        {
            VersionGroups.Add(group);
        }

        OnPropertyChanged(nameof(VersionCountText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(HasVersions));
        OnPropertyChanged(nameof(EmptyVersionText));
    }

    private string GetGroupLoader(ModVersionInfo version)
    {
        if (_selectedLoaderFilter != "全部加载器" &&
            version.Loaders.Any(loader =>
                string.Equals(ToLoaderDisplay(loader), _selectedLoaderFilter, StringComparison.OrdinalIgnoreCase)))
        {
            return _selectedLoaderFilter;
        }

        return version.Loaders.FirstOrDefault() is { } loader
            ? ToLoaderDisplay(loader)
            : "通用";
    }

    private string GetGroupGameVersion(ModVersionInfo version)
    {
        if (_selectedGameVersionFilter != "全部版本" &&
            version.GameVersions.Contains(_selectedGameVersionFilter, StringComparer.OrdinalIgnoreCase))
        {
            return _selectedGameVersionFilter;
        }

        return version.GameVersions.FirstOrDefault() ?? "通用";
    }

    private static string ToLoaderDisplay(string loader)
    {
        return loader.ToLowerInvariant() switch
        {
            "fabric" => "Fabric",
            "neoforge" => "NeoForge",
            "forge" => "Forge",
            "quilt" => "Quilt",
            _ => string.IsNullOrWhiteSpace(loader) ? "通用" : loader,
        };
    }

    private static int LoaderSortKey(string loader)
    {
        return loader.ToLowerInvariant() switch
        {
            "fabric" => 0,
            "neoforge" => 1,
            "forge" => 2,
            "quilt" => 3,
            _ => 4,
        };
    }

    private static Version GameVersionSortKey(string gameVersion)
    {
        var normalized = gameVersion.Split('-')[0];
        return Version.TryParse(normalized, out var version) ? version : new Version(0, 0);
    }

    private void RaiseProjectProperties()
    {
        OnPropertyChanged(nameof(ProjectTitle));
        OnPropertyChanged(nameof(ProjectAuthor));
        OnPropertyChanged(nameof(ProjectSource));
        OnPropertyChanged(nameof(ProjectTypeDisplay));
        OnPropertyChanged(nameof(ProjectDownloads));
        OnPropertyChanged(nameof(ProjectCategories));
        OnPropertyChanged(nameof(ProjectIconUrl));
        OnPropertyChanged(nameof(ProjectUri));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(CurrentInstanceDisplay));
        OnPropertyChanged(nameof(HasSelectedInstance));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(VersionCountText));
        OnPropertyChanged(nameof(HasVersions));
    }
}
