using System.Diagnostics;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using LiteMCLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteMCLauncher.Pages;

public sealed partial class InstancesPage : Page
{
    private bool _hasLoaded;

    public InstancesViewModel ViewModel { get; }

    public InstancesPage()
    {
        ViewModel = new InstancesViewModel(AppServices.Instances, AppServices.LauncherState);
        InitializeComponent();

        Loaded += InstancesPage_Loaded;
    }

    private async void InstancesPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await ViewModel.LoadAsync();
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

    private void LoaderFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item })
        {
            ViewModel.SetLoaderFilter(item.Content?.ToString());
        }
    }

    private void SortFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedItem: ComboBoxItem item })
        {
            ViewModel.SetSortMode(item.Content?.ToString());
        }
    }

    private async void LaunchInstance_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: MinecraftInstance instance } || XamlRoot is null)
        {
            return;
        }

        var account = AppServices.Auth.CurrentAccount;
        if (account is null)
        {
            await ShowMessageAsync("无法启动", "请先在首页登录 Microsoft 账号，或添加离线账号。");
            return;
        }

        var preflight = await AppServices.Preflight.CheckAsync(instance, account);
        if (!preflight.CanLaunch)
        {
            if (!await LaunchPreflightDialog.ShowAsync(XamlRoot, Frame, preflight))
            {
                return;
            }

            account = AppServices.Auth.CurrentAccount ?? account;
        }

        AppServices.LauncherState.SelectedInstance = instance;

        var statusText = new TextBlock
        {
            Text = "正在准备启动...",
            TextWrapping = TextWrapping.Wrap,
        };
        var progressRing = new ProgressRing
        {
            IsActive = true,
            Width = 24,
            Height = 24,
        };
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
        };
        content.Children.Add(progressRing);
        content.Children.Add(statusText);

        var dialog = new ContentDialog
        {
            Title = $"启动 {instance.DisplayName}",
            Content = content,
            CloseButtonText = "关闭",
            XamlRoot = XamlRoot,
        };

        var dialogTask = dialog.ShowAsync().AsTask();

        try
        {
            var progress = new Progress<MinecraftInstallProgress>(update =>
            {
                statusText.Text = update.Status;
            });

            var result = await AppServices.Launcher.LaunchAsync(instance, account, progress);
            dialog.Hide();
            await dialogTask;

            await AppServices.Music.HandleGameStartedAsync();

            if (AppServices.Settings.MinimizeOnLaunch &&
                App.MainWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.Minimize();
            }
        }
        catch (Exception exception)
        {
            dialog.Hide();
            await dialogTask;
            await ShowMessageAsync("启动失败", exception.Message);
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
            CloseButtonText = "知道了",
            XamlRoot = XamlRoot,
        };

        await dialog.ShowAsync();
    }

    private async void RenameInstance_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: MinecraftInstance instance } || XamlRoot is null)
        {
            return;
        }

        var nameBox = new TextBox
        {
            Text = instance.DisplayName,
            MaxLength = 64,
            MinWidth = 360,
            PlaceholderText = "输入实例名称",
        };

        var content = new StackPanel
        {
            Spacing = 8,
            MinWidth = 380,
        };
        content.Children.Add(nameBox);
        content.Children.Add(new TextBlock
        {
            Text = "名称仅用于启动器显示，不会修改版本目录。留空可恢复为默认名称。",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
        });

        var dialog = new ContentDialog
        {
            Title = "重命名实例",
            Content = content,
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        InstanceNameStore.SetName(instance.Id, nameBox.Text);
        await ViewModel.LoadAsync();
    }

    private void ManageInstance_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MinecraftInstance instance })
        {
            Frame.Navigate(typeof(InstanceManagePage), instance);
        }
    }

    private void OpenInstanceFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string directory } || !Directory.Exists(directory))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"")
        {
            UseShellExecute = true,
        });
    }
}
