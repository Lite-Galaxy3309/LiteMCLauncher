using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using LiteMCLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteMCLauncher.Pages;

public sealed partial class DownloadsPage : Page
{
    private readonly MinecraftInstallService _installService = AppServices.Installer;
    private readonly ModLoaderInstallService _loaderService = AppServices.Loaders;

    private CancellationTokenSource? _installCancellation;
    private bool _hasLoaded;

    public DownloadsViewModel ViewModel { get; }

    public DownloadsPage()
    {
        ViewModel = new DownloadsViewModel(_installService);
        InitializeComponent();

        Loaded += DownloadsPage_Loaded;
        Unloaded += DownloadsPage_Unloaded;
    }

    private async void DownloadsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await ViewModel.LoadAsync();
    }

    private void DownloadsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _installCancellation?.Cancel();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAsync();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            ViewModel.SetSearchText(textBox.Text);
        }
    }

    private void TypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item })
        {
            ViewModel.SetTypeFilter(item.Content?.ToString());
        }
    }

    private async void LoaderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item })
        {
            await ViewModel.SetLoaderAsync(item.Content?.ToString());
        }
    }

    private async void InstallVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string versionId } || string.IsNullOrWhiteSpace(versionId))
        {
            return;
        }

        await InstallVersionAsync(versionId);
    }

    private ModLoaderType GetSelectedLoader()
    {
        var name = (LoaderBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        return name switch
        {
            "Fabric" => ModLoaderType.Fabric,
            "Forge" => ModLoaderType.Forge,
            "NeoForge" => ModLoaderType.NeoForge,
            "Quilt" => ModLoaderType.Quilt,
            _ => ModLoaderType.Vanilla,
        };
    }

    private async Task InstallVersionAsync(string versionId)
    {
        if (_installCancellation is not null || XamlRoot is null)
        {
            return;
        }

        var loader = GetSelectedLoader();
        string? requestedLoaderVersion = null;

        if (loader != ModLoaderType.Vanilla)
        {
            var pick = await PickLoaderVersionAsync(versionId, loader);
            if (!pick.Confirmed)
            {
                return;
            }

            requestedLoaderVersion = pick.Version;
        }

        var cancellation = new CancellationTokenSource();
        _installCancellation = cancellation;

        var statusText = new TextBlock
        {
            Text = $"正在准备安装 Minecraft {versionId}...",
            TextWrapping = TextWrapping.Wrap,
        };
        var currentFileText = new TextBlock
        {
            Text = string.Empty,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
        };
        var progressBar = new ProgressBar
        {
            IsIndeterminate = true,
            Minimum = 0,
            Maximum = 100,
        };
        var content = new StackPanel
        {
            Spacing = 10,
            MinWidth = 420,
        };
        content.Children.Add(statusText);
        content.Children.Add(progressBar);
        content.Children.Add(currentFileText);

        var dialog = new ContentDialog
        {
            Title = $"安装 Minecraft {versionId}",
            Content = content,
            CloseButtonText = "取消",
            XamlRoot = XamlRoot,
        };

        var dialogClosed = false;
        var completed = false;
        dialog.Closed += (_, _) =>
        {
            dialogClosed = true;
            if (completed)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        };

        var dialogTask = dialog.ShowAsync().AsTask();

        try
        {
            var progress = new Progress<MinecraftInstallProgress>(update =>
            {
                if (dialogClosed)
                {
                    return;
                }

                statusText.Text = update.Status;
                progressBar.IsIndeterminate = false;
                progressBar.Value = update.Percentage;
                currentFileText.Text = update.CurrentFile ?? string.Empty;
            });

            var result = await _loaderService.InstallAsync(
                versionId,
                loader,
                requestedLoaderVersion,
                progress,
                cancellation.Token);

            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;
            ViewModel.RefreshInstalledFlags();
            await ShowMessageAsync(
                "安装完成",
                $"{result.LoaderDisplay} 实例已安装：\n{result.VersionId}\n\n" +
                $"Minecraft 目录：\n{_installService.MinecraftDirectory}\\versions\\{result.VersionId}");
        }
        catch (OperationCanceledException)
        {
            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;
        }
        catch (Exception exception)
        {
            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;
            await ShowMessageAsync("安装失败", exception.Message);
        }
        finally
        {
            cancellation.Dispose();
            _installCancellation = null;
        }
    }

    private async Task<(bool Confirmed, string? Version)> PickLoaderVersionAsync(
        string gameVersion,
        ModLoaderType loader)
    {
        if (XamlRoot is null)
        {
            return (false, null);
        }

        IReadOnlyList<LoaderVersionOption> versions;
        try
        {
            versions = await AppServices.LoaderAvailability.GetLoaderVersionsAsync(gameVersion, loader);
        }
        catch (Exception exception)
        {
            await ShowMessageAsync("获取加载器版本失败", exception.Message);
            return (false, null);
        }

        if (versions.Count == 0)
        {
            await ShowMessageAsync(
                "没有可安装的加载器版本",
                $"没有找到适用于 Minecraft {gameVersion} 的 {loader} 版本。请先切换游戏版本或加载器。");
            return (false, null);
        }

        var combo = new ComboBox
        {
            ItemsSource = versions,
            DisplayMemberPath = nameof(LoaderVersionOption.Display),
            SelectedIndex = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                new TextBlock
                {
                    Text = $"Minecraft {gameVersion} · 检测到 {versions.Count} 个可安装版本（稳定 / 推荐优先）",
                    TextWrapping = TextWrapping.Wrap,
                },
                combo,
                new TextBlock
                {
                    Text = "选择要安装的加载器构建版本，然后点击“安装”。",
                    TextWrapping = TextWrapping.Wrap,
                },
            },
        };

        var dialog = new ContentDialog
        {
            Title = $"选择 {loader} 版本",
            Content = content,
            PrimaryButtonText = "安装",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return (false, null);
        }

        var selected = combo.SelectedItem as LoaderVersionOption;
        return selected is null ? (false, null) : (true, selected.Version);
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

        await dialog.ShowAsync();
    }
}
