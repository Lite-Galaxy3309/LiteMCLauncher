using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using LiteMCLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LiteMCLauncher.Pages;

public sealed partial class ModsPage : Page
{
    private readonly ModpackInstallService _modpackService = AppServices.Modpacks;

    private CancellationTokenSource? _installCancellation;
    private CancellationTokenSource? _modpackCancellation;
    private bool _hasLoaded;

    public ModsViewModel ViewModel { get; }

    public ModsPage()
    {
        ViewModel = new ModsViewModel(
            AppServices.Mods,
            AppServices.Instances,
            AppServices.LauncherState,
            AppServices.Settings);
        InitializeComponent();

        Loaded += ModsPage_Loaded;
        Unloaded += ModsPage_Unloaded;
    }

    private async void ModsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await ViewModel.InitializeAsync();
    }

    private void ModsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _installCancellation?.Cancel();
        _modpackCancellation?.Cancel();
    }

    private void ContentTypeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item })
        {
            ViewModel.SetContentType(item.Content?.ToString() == "整合包" ? "modpack" : "mod");
        }
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        await ViewModel.SearchAsync(SearchBox.Text, resetPage: true);
    }

    private async void PreviousPage_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PreviousPageAsync();
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.NextPageAsync();
    }

    private void ModList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ModSearchResult project)
        {
            Frame.Navigate(typeof(ModDetailsPage), project);
        }
    }

    private void OpenModDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ModSearchResult project })
        {
            Frame.Navigate(typeof(ModDetailsPage), project);
        }
    }

    private async void InstallMod_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ModSearchResult project })
        {
            return;
        }

        if (project.IsModpack)
        {
            await InstallModpackAsync(project);
            return;
        }

        await InstallModAsync(project);
    }

    private async Task InstallModpackAsync(ModSearchResult project)
    {
        if (_modpackCancellation is not null || XamlRoot is null)
        {
            return;
        }

        var nameBox = new TextBox
        {
            Text = project.Title,
            MaxLength = 64,
            MinWidth = 360,
            PlaceholderText = "给这个整合包实例起个名字",
        };

        var nameContent = new StackPanel
        {
            Spacing = 8,
            MinWidth = 380,
        };
        nameContent.Children.Add(new TextBlock
        {
            Text = "安装完成后，实例会以这个名称显示在首页和实例管理中。",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
        });
        nameContent.Children.Add(nameBox);

        var nameDialog = new ContentDialog
        {
            Title = "命名整合包实例",
            Content = nameContent,
            PrimaryButtonText = "开始安装",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await nameDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var instanceName = string.IsNullOrWhiteSpace(nameBox.Text)
            ? project.Title
            : nameBox.Text.Trim();

        var cancellation = new CancellationTokenSource();
        _modpackCancellation = cancellation;

        var statusText = new TextBlock
        {
            Text = $"正在准备一键安装 {project.Title}...",
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
            MinWidth = 440,
        };
        content.Children.Add(statusText);
        content.Children.Add(progressBar);

        var dialog = new ContentDialog
        {
            Title = $"一键安装 {project.Title}",
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
            var progress = new Progress<ModpackInstallProgress>(update =>
            {
                if (dialogClosed)
                {
                    return;
                }

                statusText.Text = update.Status;
                progressBar.IsIndeterminate = false;
                progressBar.Value = update.Percentage;
            });

            var result = await _modpackService.InstallAsync(project, progress, cancellation.Token);

            var savedName = InstanceNameStore.SetName(result.InstanceId, instanceName) ?? instanceName;
            if (AppServices.LauncherState.SelectedInstance is { } selected &&
                selected.Id == result.InstanceId)
            {
                AppServices.LauncherState.SelectedInstance = selected with { CustomName = savedName };
            }

            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;
            ViewModel.RefreshInstance();
            await ShowMessageAsync(
                "整合包安装完成",
                $"{result.ModpackName}\n实例名称：{savedName}\n实例 ID：{result.InstanceId}\n" +
                $"加载器：{result.LoaderDisplay}\n" +
                $"已下载 {result.FileCount} 个整合包文件。\n\n" +
                "已自动设为当前实例，可在主页启动。");
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
            await ShowMessageAsync("整合包安装失败", exception.Message);
        }
        finally
        {
            cancellation.Dispose();
            _modpackCancellation = null;
        }
    }

    private async Task InstallModAsync(ModSearchResult project)
    {
        if (_installCancellation is not null || XamlRoot is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _installCancellation = cancellation;

        var statusText = new TextBlock
        {
            Text = $"正在获取 {project.Title} 的下载文件...",
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
            Title = project.IsModpack ? "下载整合包" : "安装 Mod",
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

            var result = await ViewModel.InstallAsync(project, progress, cancellation.Token);

            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;

            await ShowMessageAsync(
                result.IsModpack ? "整合包已下载" : "Mod 安装完成",
                result.IsModpack
                    ? $"整合包文件已下载到：\n{result.FilePath}\n版本：{result.VersionName}\n\n请使用整合包安装器导入。"
                    : $"已安装到当前实例的 mods 目录：\n{result.FilePath}\n版本：{result.VersionName}");
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
