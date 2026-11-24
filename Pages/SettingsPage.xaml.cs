using System.Diagnostics;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace LiteMCLauncher.Pages;

public sealed partial class SettingsPage : Page
{
    private static readonly string[] GameLanguageCodes = ["zh_cn", "zh_tw", "en_us", "ja_jp"];

    private readonly JavaService _javaService = AppServices.Java;
    private readonly LauncherSettingsService _settingsService = AppServices.Settings;

    private CancellationTokenSource? _javaCancellation;
    private bool _hasLoaded;
    private bool _loadingJavaChoices;
    private string? _accentColor;
    private string? _backgroundColor;
    private string? _backgroundImagePath;

    public SettingsPage()
    {
        InitializeComponent();

        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        LoadSettings();
        await LoadInstalledJavaAsync();
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _javaCancellation?.Cancel();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Current;

        JavaPathBox.Text = settings.JavaPath ?? string.Empty;
        MaxMemoryBox.Value = _settingsService.MaximumRamMb;
        JvmArgumentsBox.Text = settings.ExtraJvmArguments ?? string.Empty;

        var languageIndex = Array.IndexOf(GameLanguageCodes, _settingsService.GameLanguage);
        GameLanguageBox.SelectedIndex = languageIndex >= 0 ? languageIndex : 0;
        LauncherLanguageBox.SelectedIndex = _settingsService.Current.LauncherLanguage == "en-US" ? 1 : 0;

        DownloadSourceBox.SelectedIndex = _settingsService.DownloadSource == "BMCLAPI" ? 1 : 0;
        ThemeBox.SelectedIndex = _settingsService.Theme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0,
        };

        MinimizeOnLaunchToggle.IsOn = _settingsService.MinimizeOnLaunch;
        GameDirectoryBox.Text = AppPaths.MinecraftDirectory;

        _accentColor = settings.AccentColor;
        _backgroundColor = settings.BackgroundColor;
        _backgroundImagePath = settings.BackgroundImagePath;
        JavaSourceBox.SelectedIndex = _settingsService.JavaDownloadSource == "Adoptium" ? 1 : 0;
        TransparencyToggle.IsOn = settings.EnableTransparency;
        BackgroundModeBox.SelectedIndex = settings.BackgroundMode switch
        {
            "Solid" => 1,
            "Image" => 2,
            _ => 0,
        };
        BackgroundImageText.Text = string.IsNullOrWhiteSpace(_backgroundImagePath)
            ? "未选择图片"
            : Path.GetFileName(_backgroundImagePath);
        UpdateAccentSwatch();
        UpdateBackgroundSwatch();
    }

    private async void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        var javaPath = JavaPathBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(javaPath) &&
            !File.Exists(javaPath) &&
            !Directory.Exists(javaPath))
        {
            SettingsStatusText.Text = "Java 路径无效：请选择 java.exe 或 Java 安装目录。";
            return;
        }

        var maxMemory = MaxMemoryBox.Value;
        if (double.IsNaN(maxMemory) || maxMemory <= 0)
        {
            maxMemory = 4096;
        }

        var settings = _settingsService.Current;
        settings.JavaPath = string.IsNullOrWhiteSpace(javaPath) ? null : javaPath;
        settings.MaximumRamMb = (int)Math.Clamp(maxMemory, 1024, 32768);
        settings.GameLanguage = GameLanguageCodes[
            Math.Clamp(GameLanguageBox.SelectedIndex, 0, GameLanguageCodes.Length - 1)];
        settings.LauncherLanguage = LauncherLanguageBox.SelectedIndex == 1 ? "en-US" : "zh-CN";
        settings.ExtraJvmArguments = string.IsNullOrWhiteSpace(JvmArgumentsBox.Text)
            ? null
            : JvmArgumentsBox.Text.Trim();
        settings.DownloadSource = DownloadSourceBox.SelectedIndex == 1 ? "BMCLAPI" : "Mojang";
        settings.Theme = ThemeBox.SelectedIndex switch
        {
            1 => "Light",
            2 => "Dark",
            _ => "System",
        };
        settings.MinimizeOnLaunch = MinimizeOnLaunchToggle.IsOn;
        settings.JavaDownloadSource = JavaSourceBox.SelectedIndex == 1 ? "Adoptium" : "TUNA";
        settings.EnableTransparency = TransparencyToggle.IsOn;
        settings.AccentColor = _accentColor;
        settings.BackgroundColor = _backgroundColor;
        settings.BackgroundImagePath = _backgroundImagePath;
        settings.BackgroundMode = BackgroundModeBox.SelectedIndex switch
        {
            1 => "Solid",
            2 => "Image",
            _ => "Default",
        };

        _settingsService.Save();
        LauncherLanguageService.SetLanguage(settings.LauncherLanguage);
        if (App.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ApplyLanguage();
        }
        else
        {
            LauncherLanguageService.Apply(App.MainWindow?.Content as FrameworkElement);
        }
        PersonalizationService.Apply(refreshExistingControls: true);
        SettingsStatusText.Text = LauncherLanguageService.IsEnglish
            ? "Settings saved. Java, memory and JVM arguments apply the next time you launch the game."
            : "设置已保存。Java、内存与 JVM 参数将在下次启动游戏时生效。";
        await LoadInstalledJavaAsync();
    }

    private async void PickAccentColor_Click(object sender, RoutedEventArgs e)
    {
        var initial = PersonalizationService.ParseColor(_accentColor)
                      ?? new UISettings().GetColorValue(UIColorType.Accent);
        var color = await PickColorAsync("选择主题色", initial);
        if (color is not null)
        {
            _accentColor = PersonalizationService.ToHex(color.Value);
            UpdateAccentSwatch();
        }
    }

    private void ResetAccentColor_Click(object sender, RoutedEventArgs e)
    {
        _accentColor = null;
        UpdateAccentSwatch();
    }

    private async void PickBackgroundColor_Click(object sender, RoutedEventArgs e)
    {
        var initial = PersonalizationService.ParseColor(_backgroundColor)
                      ?? Colors.DimGray;
        var color = await PickColorAsync("选择背景色", initial);
        if (color is not null)
        {
            _backgroundColor = PersonalizationService.ToHex(color.Value);
            UpdateBackgroundSwatch();
        }
    }

    private async void PickBackgroundImage_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null || XamlRoot is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".webp");
        picker.FileTypeFilter.Add(".bmp");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var backgroundsDirectory = Path.Combine(AppPaths.DataDirectory, "backgrounds");
            Directory.CreateDirectory(backgroundsDirectory);

            var extension = Path.GetExtension(file.Path);
            var destination = Path.Combine(backgroundsDirectory, $"custom-background{extension}");
            File.Copy(file.Path, destination, overwrite: true);

            _backgroundImagePath = destination;
            BackgroundImageText.Text = Path.GetFileName(file.Name);
            BackgroundModeBox.SelectedIndex = 2;
        }
        catch (Exception exception)
        {
            SettingsStatusText.Text = $"无法复制背景图片：{exception.Message}";
        }
    }

    private void BackgroundMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // The actual background is applied when the user saves the settings.
    }

    private async Task<Color?> PickColorAsync(string title, Color initialColor)
    {
        if (XamlRoot is null)
        {
            return null;
        }

        var picker = new ColorPicker
        {
            MinWidth = 360,
            IsAlphaEnabled = false,
            IsColorChannelTextInputVisible = true,
            IsColorSliderVisible = true,
            IsColorSpectrumVisible = true,
            Color = initialColor,
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = picker,
            PrimaryButtonText = "应用",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        return await dialog.ShowLocalizedAsync() == ContentDialogResult.Primary
            ? picker.Color
            : null;
    }

    private void UpdateAccentSwatch()
    {
        var color = PersonalizationService.ParseColor(_accentColor)
                    ?? new UISettings().GetColorValue(UIColorType.Accent);
        AccentColorSwatch.Background = new SolidColorBrush(color);
        AccentColorText.Text = string.IsNullOrWhiteSpace(_accentColor) ? "跟随系统" : _accentColor;
    }

    private void UpdateBackgroundSwatch()
    {
        var color = PersonalizationService.ParseColor(_backgroundColor);
        if (color is null &&
            Application.Current.Resources["SolidBackgroundFillColorBaseBrush"] is SolidColorBrush brush)
        {
            color = brush.Color;
        }

        BackgroundColorSwatch.Background = new SolidColorBrush(color ?? Colors.DimGray);
    }

    private async void BrowseJavaPath_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            JavaPathBox.Text = file.Path;
        }
    }

    private void OpenGameFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.MinecraftDirectory);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.MinecraftDirectory}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            SettingsStatusText.Text = $"无法打开游戏目录：{exception.Message}";
        }
    }

    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var removed = 0;

        foreach (var directoryName in new[] { "cache", "webcache2" })
        {
            var directory = Path.Combine(AppPaths.MinecraftDirectory, directoryName);
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                    removed++;
                }
            }
            catch (Exception exception)
            {
                SettingsStatusText.Text = $"清理缓存失败：{exception.Message}";
                return;
            }
        }

        SettingsStatusText.Text = removed == 0
            ? "没有需要清理的缓存。"
            : $"已清理 {removed} 个缓存目录。";
    }

    private async void InstallJava8_Click(object sender, RoutedEventArgs e)
    {
        await InstallJavaAsync(8);
    }

    private async void InstallJava17_Click(object sender, RoutedEventArgs e)
    {
        await InstallJavaAsync(17);
    }

    private async void InstallJava21_Click(object sender, RoutedEventArgs e)
    {
        await InstallJavaAsync(21);
    }

    private async void InstallJava25_Click(object sender, RoutedEventArgs e)
    {
        await InstallJavaAsync(25);
    }

    private void JavaRuntimeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingJavaChoices)
        {
            return;
        }

        if (sender is ComboBox { SelectedItem: JavaChoice choice })
        {
            JavaPathBox.Text = choice.Path ?? string.Empty;
        }
    }

    private void AutoDetectJava_Click(object sender, RoutedEventArgs e)
    {
        JavaPathBox.Text = string.Empty;
        _loadingJavaChoices = true;
        try
        {
            JavaRuntimeBox.SelectedIndex = 0;
        }
        finally
        {
            _loadingJavaChoices = false;
        }

        JavaStatusText.Text = "已切换为自动检测。点击“保存设置”后生效。";
    }

    private async void RefreshJava_Click(object sender, RoutedEventArgs e)
    {
        JavaStatusText.Text = "正在重新检测本机 Java...";
        await LoadInstalledJavaAsync();
    }

    private async Task InstallJavaAsync(int featureVersion)
    {
        if (_javaCancellation is not null || XamlRoot is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _javaCancellation = cancellation;

        var source = JavaSourceBox.SelectedIndex == 1 ? "Adoptium" : "TUNA";
        _settingsService.JavaDownloadSource = source;
        var sourceDisplay = source == "Adoptium" ? "Adoptium 官方源" : "清华 TUNA 镜像";

        var statusText = new TextBlock
        {
            Text = $"正在从{sourceDisplay}获取 Java {featureVersion} 的最新版本信息…",
            TextWrapping = TextWrapping.Wrap,
        };

        var progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Minimum = 0,
            Maximum = 100,
        };

        var content = new StackPanel
        {
            Spacing = 12,
            MinWidth = 420,
        };
        content.Children.Add(statusText);
        content.Children.Add(progressBar);

        var dialog = new ContentDialog
        {
            Title = $"安装 Java {featureVersion}",
            Content = content,
            CloseButtonText = "取消",
            XamlRoot = XamlRoot,
        };

        var dialogClosed = false;
        var completed = false;

        dialog.Closed += (_, _) =>
        {
            dialogClosed = true;
            if (!completed)
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The install flow already completed and disposed its source.
                }
            }
        };

        var dialogTask = dialog.ShowLocalizedAsync();

        try
        {
            var progress = new Progress<JavaDownloadProgress>(update =>
            {
                if (dialogClosed)
                {
                    return;
                }

                statusText.Text = update.Status;
                progressBar.IsIndeterminate = false;
                progressBar.Value = update.Percentage;
            });

            var runtime = await _javaService.InstallAsync(featureVersion, progress, cancellation.Token, source);

            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;
            await LoadInstalledJavaAsync();
            await ShowMessageAsync(
                "Java 安装完成",
                $"{runtime.Name}\n版本：{runtime.Version}\n目录：{runtime.HomePath}");
        }
        catch (OperationCanceledException)
        {
            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;
            JavaStatusText.Text = "已取消 Java 安装。";
        }
        catch (Exception exception)
        {
            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;
            await ShowMessageAsync("Java 安装失败", GetErrorMessage(exception));
        }
        finally
        {
            cancellation.Dispose();
            _javaCancellation = null;
        }
    }

    private async Task LoadInstalledJavaAsync()
    {
        try
        {
            var runtimes = await _javaService.GetInstalledRuntimesAsync();

            var choices = new List<JavaChoice>
            {
                new("自动检测（推荐）", null),
            };

            foreach (var runtime in runtimes)
            {
                choices.Add(new JavaChoice(
                    $"Java {runtime.Version} · {runtime.Name}",
                    runtime.PreferredExecutable));
            }

            var currentPath = JavaPathBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(currentPath) &&
                !choices.Any(choice =>
                    string.Equals(choice.Path, currentPath, StringComparison.OrdinalIgnoreCase)))
            {
                choices.Add(new JavaChoice($"自定义：{currentPath}", currentPath));
            }

            _loadingJavaChoices = true;
            try
            {
                JavaRuntimeBox.ItemsSource = choices;
                JavaRuntimeBox.SelectedItem = choices.FirstOrDefault(choice =>
                                                string.Equals(choice.Path, currentPath, StringComparison.OrdinalIgnoreCase))
                                            ?? choices[0];
            }
            finally
            {
                _loadingJavaChoices = false;
            }

            JavaStatusText.Text = runtimes.Count == 0
                ? $"尚未安装由启动器管理的 Java。安装目录：{_javaService.JavaRoot}"
                : $"已安装 {runtimes.Count} 个 Java 运行时：" +
                  $"{string.Join("、", runtimes.Select(runtime => $"{runtime.Name}（{runtime.Version}）"))}。" +
                  $"安装目录：{_javaService.JavaRoot}";
        }
        catch (Exception exception)
        {
            JavaStatusText.Text = $"检测 Java 运行时失败：{exception.Message}";
        }
    }

    private void OpenJavaFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_javaService.JavaRoot);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_javaService.JavaRoot}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            JavaStatusText.Text = $"无法打开 Java 目录：{exception.Message}";
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
            },
            CloseButtonText = "确定",
            XamlRoot = XamlRoot,
        };

        await dialog.ShowLocalizedAsync();
    }

    private static string GetErrorMessage(Exception exception)
    {
        return string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
    }

    public sealed record JavaChoice(string Display, string? Path);
}
