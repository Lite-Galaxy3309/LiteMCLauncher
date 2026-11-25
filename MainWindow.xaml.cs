using LiteMCLauncher.Models;
using LiteMCLauncher.Pages;
using LiteMCLauncher.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace LiteMCLauncher;

public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Maps a NavigationView item tag to the page it opens.
    /// </summary>
    private static readonly Dictionary<string, Type> Routes = new()
    {
        ["home"] = typeof(HomePage),
        ["instances"] = typeof(InstancesPage),
        ["downloads"] = typeof(DownloadsPage),
        ["mods"] = typeof(ModsPage),
        ["music"] = typeof(MusicPage),
        ["online-music"] = typeof(OnlineMusicPage),
        ["news"] = typeof(NewsPage),
        ["tools"] = typeof(ToolsPage),
        ["settings"] = typeof(SettingsPage),
        ["about"] = typeof(AboutPage),
    };

    private bool _isSyncingSelection;
    private long _lastIncrementalLanguageTick;

    public MainWindow()
    {
        InitializeComponent();
        try
        {
            LauncherLanguageService.SetLanguage(AppServices.Settings.Current.LauncherLanguage);
        }
        catch
        {
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIconRounded.ico");
            AppWindow.SetIcon(File.Exists(iconPath) ? iconPath : "Assets/AppIconRounded.ico");
        }
        catch
        {
            // The exe icon still acts as a fallback.
        }

        SetInitialWindowSize();

        NavFrame.Navigated += NavFrame_Navigated;
        NavigateTo("home");
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        LauncherLanguageService.Apply(Content as FrameworkElement);

        foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Content is string content)
            {
                item.Content = LauncherLanguageService.Translate(content);
            }
        }

        if (NavView.FooterMenuItems is not null)
        {
            foreach (var item in NavView.FooterMenuItems.OfType<NavigationViewItem>())
            {
                if (item.Content is string content)
                {
                    item.Content = LauncherLanguageService.Translate(content);
                }
            }
        }
    }

    /// <summary>
    /// Applies the transparency, accent and background personalization.
    /// </summary>
    public void ApplyWindowBackground(LauncherSettings settings)
    {
        var hasImage = string.Equals(settings.BackgroundMode, "Image", StringComparison.OrdinalIgnoreCase) &&
                       !string.IsNullOrWhiteSpace(settings.BackgroundImagePath) &&
                       File.Exists(settings.BackgroundImagePath);

        BackgroundImage.Visibility = Visibility.Collapsed;
        BackgroundImage.Source = null;
        RootLayout.Background = null;

        var useMica = settings.EnableTransparency &&
                      string.Equals(settings.BackgroundMode, "Default", StringComparison.OrdinalIgnoreCase);

        try
        {
            SystemBackdrop = useMica ? new MicaBackdrop() : null;
        }
        catch
        {
            SystemBackdrop = null;
        }

        if (hasImage)
        {
            try
            {
                BackgroundImage.Source = new BitmapImage(new Uri(settings.BackgroundImagePath!));
                BackgroundImage.Visibility = Visibility.Visible;
            }
            catch
            {
                BackgroundImage.Visibility = Visibility.Collapsed;
            }

            RootLayout.Background = new SolidColorBrush(ParseColor(settings.BackgroundColor) ?? Colors.Black);
            return;
        }

        if (string.Equals(settings.BackgroundMode, "Solid", StringComparison.OrdinalIgnoreCase) ||
            !settings.EnableTransparency)
        {
            RootLayout.Background = new SolidColorBrush(
                ParseColor(settings.BackgroundColor) ?? GetThemeBackgroundColor());
        }
    }

    private static Color? ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim().TrimStart('#');
        if (text.Length == 6)
        {
            text = "FF" + text;
        }

        if (text.Length != 8)
        {
            return null;
        }

        return uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var argb)
            ? Color.FromArgb(
                (byte)((argb >> 24) & 0xFF),
                (byte)((argb >> 16) & 0xFF),
                (byte)((argb >> 8) & 0xFF),
                (byte)(argb & 0xFF))
            : null;
    }

    private Color GetThemeBackgroundColor()
    {
        if (Application.Current.Resources["SolidBackgroundFillColorBaseBrush"] is SolidColorBrush brush)
        {
            return brush.Color;
        }

        return RootLayout.ActualTheme == ElementTheme.Dark
            ? Color.FromArgb(255, 32, 32, 32)
            : Colors.White;
    }

    private void SetInitialWindowSize()
    {
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var width = Math.Min(1120, workArea.Width - 64);
        var height = Math.Min(760, workArea.Height - 16);

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height));
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack)
        {
            NavFrame.GoBack();
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isSyncingSelection)
        {
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            NavigateTo(tag);
        }
    }

    private static async Task ApplyPageLanguageAsync(FrameworkElement page)
    {
        LauncherLanguageService.Apply(page);
        await Task.Delay(250);
        LauncherLanguageService.Apply(page);
        await Task.Delay(900);
        LauncherLanguageService.Apply(page);
    }

    private void NavFrame_Navigated(object sender, NavigationEventArgs e)
    {
        AppTitleBar.IsBackButtonVisible = NavFrame.CanGoBack;
        try
        {
            LauncherLanguageService.Apply(NavFrame.Content as FrameworkElement);
            if (NavFrame.Content is FrameworkElement page)
            {
                page.Loaded += (_, _) => LauncherLanguageService.ApplyNew(page);
                page.LayoutUpdated += (_, _) =>
                {
                    if (!LauncherLanguageService.IsEnglish)
                    {
                        return;
                    }

                    var now = Environment.TickCount64;
                    if (now - _lastIncrementalLanguageTick < 250)
                    {
                        return;
                    }

                    _lastIncrementalLanguageTick = now;
                    LauncherLanguageService.ApplyNew(page);
                };
                _ = ApplyPageLanguageAsync(page);
            }
        }
        catch
        {
        }

        var tag = Routes.FirstOrDefault(route => route.Value == e.SourcePageType).Key;
        if (string.IsNullOrEmpty(tag))
        {
            return;
        }

        var item = NavView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(menuItem => string.Equals(menuItem.Tag as string, tag, StringComparison.Ordinal));

        if (item is null || ReferenceEquals(NavView.SelectedItem, item))
        {
            return;
        }

        _isSyncingSelection = true;
        try
        {
            NavView.SelectedItem = item;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void NavigateTo(string tag)
    {
        if (!Routes.TryGetValue(tag, out var pageType))
        {
            return;
        }

        if (NavFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        NavFrame.Navigate(pageType, null, new EntranceNavigationTransitionInfo());
    }
}
