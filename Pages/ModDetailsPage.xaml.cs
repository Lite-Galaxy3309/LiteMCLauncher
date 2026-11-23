using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using LiteMCLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LiteMCLauncher.Pages;

public sealed partial class ModDetailsPage : Page
{
    private CancellationTokenSource? _downloadCancellation;

    public ModDetailsViewModel ViewModel { get; }

    public ModDetailsPage()
    {
        ViewModel = new ModDetailsViewModel(
            AppServices.Mods,
            AppServices.LauncherState,
            AppServices.Instances,
            AppServices.Settings);
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is ModSearchResult project)
        {
            await ViewModel.LoadAsync(project);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _downloadCancellation?.Cancel();
        base.OnNavigatedFrom(e);
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void InstallLatest_Click(object sender, RoutedEventArgs e)
    {
        var version = ViewModel.LatestVersion;
        if (version is null)
        {
            await ShowMessageAsync("无法安装", "当前没有可下载的版本。");
            return;
        }

        await InstallVersionAsync(version);
    }

    private async void DownloadVersion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ModVersionInfo version })
        {
            await InstallVersionAsync(version);
        }
    }

    private async Task InstallVersionAsync(ModVersionInfo version)
    {
        if (_downloadCancellation is not null || XamlRoot is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _downloadCancellation = cancellation;

        var statusText = new TextBlock
        {
            Text = $"正在准备下载 {version.DisplayName}...",
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
            Spacing = 10,
            MinWidth = 420,
        };
        content.Children.Add(statusText);
        content.Children.Add(progressBar);

        var dialog = new ContentDialog
        {
            Title = "下载并安装",
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
            var progress = new Progress<ModDownloadProgress>(update =>
            {
                if (dialogClosed)
                {
                    return;
                }

                statusText.Text = update.Status;
                progressBar.IsIndeterminate = false;
                progressBar.Value = update.Percentage;
            });

            var filePath = await ViewModel.DownloadVersionAsync(version, progress, cancellation.Token);

            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;
            await ShowMessageAsync("下载完成", $"文件已保存到：\n{filePath}");
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
            await ShowMessageAsync("下载失败", exception.Message);
        }
        finally
        {
            cancellation.Dispose();
            _downloadCancellation = null;
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

        await dialog.ShowAsync();
    }
}
