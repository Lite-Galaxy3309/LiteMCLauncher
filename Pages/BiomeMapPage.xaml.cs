using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Styles;

namespace LiteMCLauncher.Pages;

public sealed partial class BiomeMapPage : Page, INotifyPropertyChanged
{
    private bool _isRendering;
    private string _statusText = "输入种子后生成群系地图。";
    private SeedVersionGroup? _selectedVersion;
    private MRect? _mapExtent;
    private long _currentSeed;
    private SeedVersionGroup? _currentVersion;
    private MPoint? _pinWorld;

    public BiomeMapPage()
    {
        InitializeComponent();
        Legend =
        [
            new BiomeLegendItem(LauncherLanguageService.IsEnglish ? "Plains" : "平原", Colors.LightGreen),
            new BiomeLegendItem(LauncherLanguageService.IsEnglish ? "Ocean" : "海洋", Colors.CornflowerBlue),
            new BiomeLegendItem(LauncherLanguageService.IsEnglish ? "Desert" : "沙漠", Colors.Khaki),
            new BiomeLegendItem(LauncherLanguageService.IsEnglish ? "Forest" : "森林", Colors.ForestGreen),
            new BiomeLegendItem(LauncherLanguageService.IsEnglish ? "Snowy Plains" : "雪原", Colors.White),
        ];
        MapView.Map = new Mapsui.Map
        {
            BackColor = Mapsui.Styles.Color.Transparent,
        };
        MapView.Map.Navigator.ZoomLock = true;
        MapView.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Transparent);
        MapView.SizeChanged += (_, _) =>
        {
            FitMap();
            PositionPin();
        };
        MapView.Map.Tapped += Map_Tapped;
        Versions = SeedFinderService.GetVersionGroups();
        SelectedVersion = Versions.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<SeedVersionGroup> Versions { get; }

    public ObservableCollection<BiomeLegendItem> Legend { get; }

    public SeedVersionGroup? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (Equals(_selectedVersion, value)) return;
            _selectedVersion = value;
            OnPropertyChanged();
        }
    }

    public bool IsRendering
    {
        get => _isRendering;
        private set
        {
            if (_isRendering == value) return;
            _isRendering = value;
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

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
        else Frame.Navigate(typeof(ToolsPage));
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (IsRendering) return;
        var seedText = SeedBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(seedText))
        {
            StatusText = LauncherLanguageService.IsEnglish ? "Enter a seed." : "请输入种子。";
            return;
        }

        if (SelectedVersion is null)
        {
            StatusText = LauncherLanguageService.IsEnglish ? "Select a version." : "请选择版本。";
            return;
        }

        IsRendering = true;
        StatusText = LauncherLanguageService.IsEnglish ? "Generating biome map…" : "正在生成群系地图…";
        var version = SelectedVersion;

        try
        {
            var seed = RZeroF1.Minecraft.SeedMap.Api.ParseSeed(seedText);
            var result = await BiomeMapService.GenerateAsync(
                seed,
                version,
                (int)Math.Clamp(RadiusBox.Value, 1, 1_875_000));
            _currentSeed = seed;
            _currentVersion = version;
            ApplyResult(result, fit: true);

            StatusText = version.IsApproximate
                ? (LauncherLanguageService.IsEnglish
                    ? "Generated (approximate 1.21.11 profile)."
                    : "已生成（使用 1.21.11 近似规则）。")
                : (LauncherLanguageService.IsEnglish
                    ? $"Generated {result.Legend.Count} biomes."
                    : $"已生成 {result.Legend.Count} 种群系。");
        }
        catch (Exception exception)
        {
            StatusText = LauncherLanguageService.IsEnglish
                ? $"Failed to generate: {exception.Message}"
                : $"生成失败：{exception.Message}";
        }
        finally
        {
            IsRendering = false;
        }
    }

    private void ApplyResult(BiomeMapResult result, bool fit)
    {
        var extent = new MRect(result.MinX, result.MinZ, result.MaxX, result.MaxZ);
        var raster = new MRaster(result.ImageBytes, extent);
        var feature = new RasterFeature(raster);
        feature.Styles.Add(new RasterStyle());
        var layer = new MemoryLayer("Biome map")
        {
            Features = [feature],
        };

        MapView.Map.Layers.Clear();
        MapView.Map.Layers.Add(layer);
        Legend.Clear();
        foreach (var item in result.Legend)
        {
            Legend.Add(new BiomeLegendItem(item.Name, item.Color));
        }

        if (fit)
        {
            _mapExtent = extent;
            FitMap();
        }

        MapView.ForceUpdate();
    }

    private void FitMap()
    {
        if (_mapExtent is null) return;
        var width = Math.Max(1, MapView.ActualWidth);
        var height = Math.Max(1, MapView.ActualHeight);
        var extent = _mapExtent;
        var maxResolution = Math.Min(extent.Width / width, extent.Height / height);
        var halfVisibleWidth = width * maxResolution / 2;
        var halfVisibleHeight = height * maxResolution / 2;
        var panBounds = new MRect(
            extent.Min.X + halfVisibleWidth,
            extent.Min.Y + halfVisibleHeight,
            extent.Max.X - halfVisibleWidth,
            extent.Max.Y - halfVisibleHeight);
        MapView.Map.Navigator.OverridePanBounds = panBounds;
        MapView.Map.Navigator.CenterOnAndZoomTo(new MPoint(0, 0), maxResolution);
    }

    private async void Map_Tapped(object? sender, MapEventArgs e)
    {
        if (_currentVersion is null) return;
        var x = (int)Math.Floor(e.WorldPosition.X);
        var z = (int)Math.Floor(e.WorldPosition.Y);
        _pinWorld = new MPoint(x + 0.5, z + 0.5);
        PinText.Text = LauncherLanguageService.IsEnglish
            ? $"X {x}  Z {z}\nLoading biome…"
            : $"X {x}  Z {z}\n正在读取群系…";
        PinPopup.Visibility = Visibility.Visible;
        PositionPin();

        try
        {
            var biome = await BiomeMapService.GetBiomeAtAsync(_currentSeed, _currentVersion, x, z);
            if (_pinWorld is null) return;
            PinText.Text = biome is null
                ? (LauncherLanguageService.IsEnglish ? $"X {x}  Z {z}\nUnknown biome" : $"X {x}  Z {z}\n未知群系")
                : (LauncherLanguageService.IsEnglish
                    ? $"X {x}  Z {z}\n{biome.Value.Name}"
                    : $"X {x}  Z {z}\n{biome.Value.Name}");
        }
        catch
        {
            PinText.Text = LauncherLanguageService.IsEnglish ? "Biome lookup failed." : "群系读取失败。";
        }
    }

    private void PositionPin()
    {
        if (_pinWorld is null || PinPopup.Visibility != Visibility.Visible) return;
        var screen = MapView.Map.Navigator.Viewport.WorldToScreen(_pinWorld.X, _pinWorld.Y);
        var left = Math.Clamp(screen.X - 105, 0, Math.Max(0, MapView.ActualWidth - 210));
        var top = Math.Clamp(screen.Y - 82, 0, Math.Max(0, MapView.ActualHeight - 82));
        Canvas.SetLeft(PinPopup, left);
        Canvas.SetTop(PinPopup, top);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
