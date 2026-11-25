using System.Collections.ObjectModel;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;

namespace LiteMCLauncher.ViewModels;

/// <summary>
/// Modrinth search with category filtering and pagination.
/// </summary>
public sealed class ModsViewModel : ObservableObject
{
    private const int PageSize = 20;

    private readonly ModPlatformService _platformService;
    private readonly MinecraftInstanceService _instanceService;
    private readonly LauncherState _launcherState;
    private readonly LauncherSettingsService _settingsService;

    private bool _isLoading;
    private bool _hasSearched;
    private string? _errorMessage;
    private string _contentType = "mod";
    private ModCategoryOption _selectedCategory;
    private string? _lastQuery;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _totalHits;

    public ModsViewModel(
        ModPlatformService platformService,
        MinecraftInstanceService instanceService,
        LauncherState launcherState,
        LauncherSettingsService settingsService)
    {
        _platformService = platformService;
        _instanceService = instanceService;
        _launcherState = launcherState;
        _settingsService = settingsService;

        Categories =
        [
            new ModCategoryOption("全部分类", null),
            new ModCategoryOption("Adventure", "adventure"),
            new ModCategoryOption("Structure", "worldgen"),
            new ModCategoryOption("Optimization", "optimization"),
            new ModCategoryOption("Decoration", "decoration"),
            new ModCategoryOption("Technology", "technology"),
            new ModCategoryOption("Magic", "magic"),
            new ModCategoryOption("Storage", "storage"),
            new ModCategoryOption("Worldgen", "worldgen"),
            new ModCategoryOption("Utility", "utility"),
            new ModCategoryOption("Library", "library"),
            new ModCategoryOption("Equipment", "equipment"),
            new ModCategoryOption("Food", "food"),
            new ModCategoryOption("Game Mechanics", "game-mechanics"),
            new ModCategoryOption("Management", "management"),
            new ModCategoryOption("Minigame", "minigame"),
            new ModCategoryOption("Mobs", "mobs"),
            new ModCategoryOption("Social", "social"),
            new ModCategoryOption("Transportation", "transportation"),
            new ModCategoryOption("Economy", "economy"),
            new ModCategoryOption("Cursed", "cursed"),
        ];

        _selectedCategory = Categories[0];
    }

    public ObservableCollection<ModSearchResult> Results { get; } = [];

    public IReadOnlyList<ModCategoryOption> Categories { get; }

    public ModCategoryOption SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (value is not null && SetProperty(ref _selectedCategory, value))
            {
                // A category change takes effect on the next search.
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
                RaiseResultProperties();
            }
        }
    }

    public bool IsEmpty => !IsLoading && Results.Count == 0;

    public bool HasSelectedInstance => _launcherState.SelectedInstance is not null;

    public string CurrentInstanceDisplay => _launcherState.SelectedInstance is { } instance
        ? $"{instance.DisplayName} · {instance.Summary}"
        : "未选择实例";

    public string ModsDirectoryDisplay => _launcherState.SelectedInstance is { } instance
        ? _launcherState.GetModsDirectory(instance)
        : "请先在实例管理中选择一个实例";

    public string ResultCountText => IsLoading
        ? "正在搜索…"
        : _hasSearched
            ? $"共 {_totalHits} 个结果"
            : "尚未搜索";

    public string StatusText => IsLoading
        ? "正在搜索 Modrinth..."
        : _errorMessage is not null
            ? "搜索失败"
            : _hasSearched
                ? $"第 {_currentPage} / {_totalPages} 页"
                : "输入关键词后开始搜索";

    public string PageText => $"第 {_currentPage} / {_totalPages} 页";

    public bool CanGoPrevious => _hasSearched && _currentPage > 1;

    public bool CanGoNext => _hasSearched && _currentPage < _totalPages;

    public string ContentType => _contentType;

    public string EmptyStateTitle
    {
        get
        {
            if (_errorMessage is not null)
            {
                return "搜索失败";
            }

            return _hasSearched ? "没有找到匹配的项目" : "搜索 Mod 或整合包";
        }
    }

    public string EmptyStateDescription => _errorMessage ?? (_hasSearched
        ? (LauncherLanguageService.IsEnglish ? "Try another keyword, category or content type." : "尝试更换关键词、分类或内容类型。")
        : (LauncherLanguageService.IsEnglish ? "Search Modrinth by keyword and switch categories or mod/modpack types." : "按关键词搜索 Modrinth，可切换分类与 Mod/整合包类型。"));

    public async Task InitializeAsync()
    {
        if (_launcherState.SelectedInstance is null)
        {
            try
            {
                var instances = await _instanceService.GetInstancesAsync();
                _launcherState.SelectedInstance = instances.FirstOrDefault();
            }
            catch
            {
                // The Mods page can still be used for searching.
            }
        }

        RaiseInstanceProperties();
    }

    public void SetContentType(string contentType)
    {
        _contentType = string.Equals(contentType, "modpack", StringComparison.OrdinalIgnoreCase)
            ? "modpack"
            : "mod";
        OnPropertyChanged(nameof(ContentType));
    }

    public void RefreshInstance()
    {
        RaiseInstanceProperties();
    }

    public async Task SearchAsync(
        string? query,
        bool resetPage = true,
        CancellationToken cancellationToken = default)
    {
        query = query?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            _errorMessage = LauncherLanguageService.IsEnglish
                ? "Enter a mod or modpack name to search."
                : "请输入要搜索的 Mod 或整合包名称。";
            _hasSearched = false;
            Results.Clear();
            RaiseResultProperties();
            return;
        }

        if (IsLoading)
        {
            return;
        }

        if (resetPage)
        {
            _currentPage = 1;
        }

        _lastQuery = query;
        IsLoading = true;
        _errorMessage = null;
        _hasSearched = true;
        Results.Clear();

        try
        {
            var page = await _platformService.SearchModrinthPageAsync(
                _contentType,
                query,
                SelectedCategory.Slug,
                (_currentPage - 1) * PageSize,
                PageSize,
                cancellationToken);

            foreach (var result in page.Results)
            {
                Results.Add(result);
            }

            _totalHits = page.TotalHits;
            _totalPages = Math.Max(1, page.TotalPages);
        }
        catch (OperationCanceledException)
        {
            _hasSearched = false;
            _errorMessage = "搜索已取消。";
        }
        catch (Exception exception)
        {
            _errorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
            RaiseResultProperties();
        }
    }

    public Task NextPageAsync(CancellationToken cancellationToken = default)
    {
        if (!CanGoNext || _lastQuery is null)
        {
            return Task.CompletedTask;
        }

        _currentPage++;
        return SearchAsync(_lastQuery, resetPage: false, cancellationToken);
    }

    public Task PreviousPageAsync(CancellationToken cancellationToken = default)
    {
        if (!CanGoPrevious || _lastQuery is null)
        {
            return Task.CompletedTask;
        }

        _currentPage--;
        return SearchAsync(_lastQuery, resetPage: false, cancellationToken);
    }

    public async Task<ModInstallResult> InstallAsync(
        ModSearchResult project,
        IProgress<ModDownloadProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var instance = _launcherState.SelectedInstance
                       ?? throw new InvalidOperationException("请先在实例管理中选择一个实例。");

        var targetDirectory = project.IsModpack
            ? _launcherState.GetModpacksDirectory(instance)
            : _launcherState.GetModsDirectory(instance);

        Directory.CreateDirectory(targetDirectory);

        return await _platformService.InstallAsync(
            project,
            instance,
            targetDirectory,
            _settingsService.CurseForgeApiKey,
            progress,
            cancellationToken);
    }

    private void RaiseInstanceProperties()
    {
        OnPropertyChanged(nameof(HasSelectedInstance));
        OnPropertyChanged(nameof(CurrentInstanceDisplay));
        OnPropertyChanged(nameof(ModsDirectoryDisplay));
    }

    private void RaiseResultProperties()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ResultCountText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(EmptyStateTitle));
        OnPropertyChanged(nameof(EmptyStateDescription));
    }
}
