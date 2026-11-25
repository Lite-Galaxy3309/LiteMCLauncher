using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteMCLauncher.Pages;

public sealed partial class SeedFinderPage : Page, INotifyPropertyChanged
{
    private CancellationTokenSource? _searchCancellation;
    private bool _isSearching;
    private string _statusText = "输入种子后开始搜索。";
    private SeedVersionGroup? _selectedVersion;
    private SeedStructureOption? _selectedStructure;

    public SeedFinderPage()
    {
        InitializeComponent();
        Loaded += SeedFinderPage_Loaded;
        Unloaded += (_, _) => _searchCancellation?.Cancel();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SeedVersionGroup> Versions { get; } = [];

    public ObservableCollection<SeedStructureOption> Structures { get; } = [];

    public ObservableCollection<SeedStructureResult> Results { get; } = [];

    public SeedVersionGroup? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (Equals(_selectedVersion, value)) return;
            _selectedVersion = value;
            OnPropertyChanged();
            LoadStructures();
        }
    }

    public SeedStructureOption? SelectedStructure
    {
        get => _selectedStructure;
        set
        {
            if (Equals(_selectedStructure, value)) return;
            _selectedStructure = value;
            OnPropertyChanged();
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (_isSearching == value) return;
            _isSearching = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value) return;
            _statusText = value;
            OnPropertyChanged();
        }
    }

    private void SeedFinderPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (Versions.Count > 0) return;
        foreach (var group in SeedFinderService.GetVersionGroups()) Versions.Add(group);
        SelectedVersion = Versions.FirstOrDefault();
    }

    private void LoadStructures()
    {
        Structures.Clear();
        SelectedStructure = null;
        if (SelectedVersion is null) return;

        foreach (var structure in SeedFinderService.GetStructures(SelectedVersion.RepresentativeKey))
        {
            Structures.Add(structure);
        }

        SelectedStructure = Structures.FirstOrDefault();
        if (Structures.Count == 0)
        {
            StatusText = LauncherLanguageService.IsEnglish
                ? "This version has no supported structures."
                : "该版本没有可用的结构。";
        }
        else if (SelectedVersion.IsApproximate)
        {
            StatusText = LauncherLanguageService.IsEnglish
                ? "Approximate profile: using 1.21.11 generation."
                : "近似版本：使用 1.21.11 的生成规则。";
        }
        else
        {
            StatusText = LauncherLanguageService.IsEnglish
                ? $"{Structures.Count} structures available."
                : $"可用结构 {Structures.Count} 个。";
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _searchCancellation?.Cancel();
        if (Frame.CanGoBack) Frame.GoBack();
        else Frame.Navigate(typeof(ToolsPage));
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _searchCancellation?.Cancel();

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        if (IsSearching) return;
        var seedText = SeedBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(seedText))
        {
            StatusText = LauncherLanguageService.IsEnglish ? "Enter a seed." : "请输入种子。";
            return;
        }

        if (SelectedVersion is null || SelectedStructure is null)
        {
            StatusText = LauncherLanguageService.IsEnglish ? "Select a version and structure." : "请选择版本和结构类型。";
            return;
        }

        var radius = RadiusBox.Value;
        if (double.IsNaN(radius) || radius < 1 || radius > 10000)
        {
            StatusText = LauncherLanguageService.IsEnglish ? "Radius must be between 1 and 10000 chunks." : "半径必须在 1 到 10000 区块之间。";
            return;
        }

        Results.Clear();
        EmptyText.Visibility = Visibility.Collapsed;
        IsSearching = true;
        StatusText = LauncherLanguageService.IsEnglish ? "Searching…" : "正在搜索…";
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();

        try
        {
            var found = await Task.Run(
                () => SeedFinderService.SearchAsync(
                    seedText,
                    SelectedVersion.RepresentativeKey,
                    SelectedStructure.Key,
                    (int)radius,
                    _searchCancellation.Token),
                _searchCancellation.Token);

            foreach (var result in found) Results.Add(result);
            if (Results.Count == 0)
            {
                EmptyText.Visibility = Visibility.Visible;
                EmptyText.Text = LauncherLanguageService.IsEnglish ? "No matching structures found." : "没有找到符合条件的结构。";
            }

            StatusText = LauncherLanguageService.IsEnglish
                ? $"Found {Results.Count} candidate structures."
                : $"找到 {Results.Count} 个候选结构。";
        }
        catch (OperationCanceledException)
        {
            StatusText = LauncherLanguageService.IsEnglish ? "Search cancelled." : "搜索已取消。";
        }
        catch (Exception exception)
        {
            StatusText = LauncherLanguageService.IsEnglish ? $"Search failed: {exception.Message}" : $"搜索失败：{exception.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
