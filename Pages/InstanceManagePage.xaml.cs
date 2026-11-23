using System.Diagnostics;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using LiteMCLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LiteMCLauncher.Pages;

public sealed partial class InstanceManagePage : Page
{
    public InstanceManageViewModel ViewModel { get; }

    public InstanceManagePage()
    {
        ViewModel = new InstanceManageViewModel(
            AppServices.InstanceContent,
            AppServices.LauncherState,
            AppServices.Java);
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        MinecraftInstance? instance = e.Parameter as MinecraftInstance;

        if (instance is null && e.Parameter is string instanceId)
        {
            instance = (await AppServices.Instances.GetInstancesAsync())
                .FirstOrDefault(item => string.Equals(item.Id, instanceId, StringComparison.Ordinal));
        }

        if (instance is not null)
        {
            await ViewModel.LoadAsync(instance);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
    }

    private void OpenInstanceDirectory_Click(object sender, RoutedEventArgs e)
    {
        OpenPath(ViewModel.GameDirectory, selectFile: false);
    }

    private async void ReloadJava_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshJavaAsync();
    }

    private void SaveInstanceSettings_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveInstanceSettings();
    }

    private async void RefreshLogs_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshLogsAsync();
    }

    private async void ChooseJava_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        if (!Path.GetFileName(file.Path).StartsWith("java", StringComparison.OrdinalIgnoreCase))
        {
            await ShowMessageAsync("无法使用该文件", "请选择 java.exe 或 javaw.exe。");
            return;
        }

        await ViewModel.SetCustomJavaPathAsync(file.Path);
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

    private void OpenItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path } && !string.IsNullOrWhiteSpace(path))
        {
            OpenPath(path, selectFile: File.Exists(path));
        }
    }

    private void OpenPath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path } && !string.IsNullOrWhiteSpace(path))
        {
            OpenPath(path, selectFile: false);
        }
    }

    private static void OpenPath(string path, bool selectFile)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (selectFile && File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true,
                });
                return;
            }

            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"")
                {
                    UseShellExecute = true,
                });
            }
        }
        catch
        {
            // Explorer can fail under unusual shell configurations.
        }
    }
}
