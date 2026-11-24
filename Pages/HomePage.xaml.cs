using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using LiteMCLauncher.ViewModels;
using Microsoft.Identity.Client;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LiteMCLauncher.Pages;

public sealed partial class HomePage : Page
{
    private readonly MicrosoftAuthService _authService = AppServices.Auth;
    private readonly GameLaunchService _launchService = AppServices.Launcher;

    private CancellationTokenSource? _loginCancellation;
    private CancellationTokenSource? _launchCancellation;
    private TextBox? _deviceCodeBox;
    private TextBlock? _deviceCodeStatus;
    private HyperlinkButton? _openLoginPageLink;
    private bool _loginCompleted;
    private bool _isLaunching;

    public HomeViewModel ViewModel { get; }

    public HomePage()
    {
        ViewModel = new HomeViewModel(
            _authService,
            AppServices.Instances,
            AppServices.LauncherState,
            AppServices.Java,
            AppServices.Settings,
            AppServices.Avatars);
        InitializeComponent();

        Loaded += HomePage_Loaded;
        Unloaded += HomePage_Unloaded;
    }

    private async void HomePage_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.Attach();
        await ViewModel.LoadAsync();
        await AppServices.Music.InitializeAsync();
    }

    private void HomePage_Unloaded(object sender, RoutedEventArgs e)
    {
        _loginCancellation?.Cancel();
        _launchCancellation?.Cancel();
        ViewModel.Dispose();
    }

    private void ManageInstance_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedInstance is { } instance)
        {
            Frame.Navigate(typeof(InstanceManagePage), instance);
        }
        else
        {
            Frame.Navigate(typeof(InstancesPage));
        }
    }

    private void OpenGameDirectory_Click(object sender, RoutedEventArgs e)
    {
        string path;

        if (ViewModel.SelectedInstance is { } instance)
        {
            AppServices.LauncherState.EnsureInstanceDirectories(instance);
            path = AppServices.LauncherState.GetGameDirectory(instance);
        }
        else
        {
            path = AppPaths.MinecraftDirectory;
        }

        OpenDirectory(path);
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        var directory = ViewModel.SelectedInstance is { } instance
            ? Path.Combine(AppServices.LauncherState.GetGameDirectory(instance), "logs")
            : Path.Combine(AppPaths.MinecraftDirectory, "logs");

        try
        {
            Directory.CreateDirectory(directory);
            var latestLog = Path.Combine(directory, "latest.log");

            if (File.Exists(latestLog))
            {
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{latestLog}\"")
                {
                    UseShellExecute = true,
                });
                return;
            }
        }
        catch
        {
        }

        OpenDirectory(directory);
    }

    private void InstallMod_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ModsPage));
    }

    private static void OpenDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"")
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // Explorer may be unavailable in unusual shell environments.
        }
    }

    private async void ImportMusic_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null || XamlRoot is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".flac");
        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".m4a");
        picker.FileTypeFilter.Add(".aac");
        picker.FileTypeFilter.Add(".ogg");
        picker.FileTypeFilter.Add(".opus");
        picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        var files = await picker.PickMultipleFilesAsync();
        if (files is null || files.Count == 0)
        {
            return;
        }

        await AppServices.Music.ImportFilesAsync(files);
    }

    private async void HomeMusic_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MusicTrack track)
        {
            await AppServices.Music.PlayTrackAsync(track);
        }
    }

    private async void RemoveHomeMusic_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MusicTrack track })
        {
            await AppServices.Music.RemoveTrackAsync(track);
        }
    }

    private async void MusicPlayPause_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.ToggleOverlayPlayPauseAsync();
    }

    private async void MusicNext_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.NextOverlayAsync();
    }

    private async void MusicPrevious_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.PreviousOverlayAsync();
    }

    private void MusicRepeatMode_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CycleOverlayRepeat();
    }

    private async void RemoveAccount_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedAccount is not { } account || XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "移除账号",
            Content = new TextBlock
            {
                Text = $"确定从启动器中移除账号“{account.Username}”吗？\n这不会删除游戏存档。",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "移除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowLocalizedAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await ViewModel.RemoveSelectedAccountAsync();
    }

    private async void OfflineButton_Click(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var usernameBox = new TextBox
        {
            Text = _authService.CurrentAccount?.IsOffline == true
                ? _authService.CurrentAccount.Username
                : "Player",
            PlaceholderText = "离线用户名",
            MaxLength = 16,
            MinWidth = 360,
        };

        var content = new StackPanel
        {
            Spacing = 10,
            MinWidth = 380,
        };
        content.Children.Add(new TextBlock
        {
            Text = "离线模式只影响本地游戏，无法加入需要正版验证的服务器。",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(usernameBox);

        var dialog = new ContentDialog
        {
            Title = "离线模式",
            Content = content,
            PrimaryButtonText = "进入离线模式",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowLocalizedAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var username = usernameBox.Text.Trim();
        var isValid = username.Length is >= 3 and <= 16 &&
                      username.All(character => char.IsLetterOrDigit(character) || character == '_');

        if (!isValid)
        {
            await ShowMessageAsync("离线模式", "用户名需要是 3-16 位字母、数字或下划线。");
            return;
        }

        await _authService.SignInOfflineAsync(username);
        ViewModel.RefreshAccounts();
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        if (_loginCancellation is not null || XamlRoot is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _loginCancellation = cancellation;
        _loginCompleted = false;
        ViewModel.BeginLogin();

        var (dialog, codeBox, status, link) = CreateLoginDialog();
        _deviceCodeBox = codeBox;
        _deviceCodeStatus = status;
        _openLoginPageLink = link;

        var dialogClosed = false;
        dialog.Closed += (_, _) =>
        {
            dialogClosed = true;
            if (!_loginCompleted)
            {
                try
                {
                    cancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // The login flow already completed and disposed its source.
                }
            }
        };

        Task? dialogTask = null;

        try
        {
            dialogTask = dialog.ShowLocalizedAsync();

            var account = await _authService.SignInWithDeviceCodeAsync(
                OnDeviceCodeReceivedAsync,
                cancellation.Token);

            _loginCompleted = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            if (dialogTask is not null)
            {
                await dialogTask;
            }

            ViewModel.EndLogin();
            await ShowMessageAsync(
                "登录成功",
                $"已登录为 {account.Username}\nUUID: {account.FormattedUuid}");
        }
        catch (OperationCanceledException)
        {
            _loginCompleted = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            if (dialogTask is not null)
            {
                await dialogTask;
            }

            ViewModel.EndLogin();
        }
        catch (Exception exception)
        {
            _loginCompleted = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            if (dialogTask is not null)
            {
                await dialogTask;
            }

            ViewModel.EndLogin();
            await ShowMessageAsync("登录失败", GetErrorMessage(exception));
        }
        finally
        {
            cancellation.Dispose();
            _loginCancellation = null;
            _deviceCodeBox = null;
            _deviceCodeStatus = null;
            _openLoginPageLink = null;
        }
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLaunching || XamlRoot is null)
        {
            return;
        }

        var instance = ViewModel.SelectedInstance;
        if (instance is null)
        {
            await ShowMessageAsync("无法启动", "请先在“实例管理”或主页下拉框中选择一个实例。");
            return;
        }

        var account = _authService.CurrentAccount;
        if (account is null)
        {
            await ShowMessageAsync("无法启动", "请先在主页登录 Microsoft 账号或添加离线账号。");
            return;
        }

        var preflight = await AppServices.Preflight.CheckAsync(instance, account);
        if (!preflight.CanLaunch)
        {
            if (!await LaunchPreflightDialog.ShowAsync(XamlRoot, Frame, preflight))
            {
                return;
            }

            account = _authService.CurrentAccount ?? account;
        }

        _isLaunching = true;
        LaunchButton.IsEnabled = false;

        var cancellation = new CancellationTokenSource();
        _launchCancellation = cancellation;

        var statusText = new TextBlock
        {
            Text = $"正在准备启动 {instance.Id}...",
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
            Title = "启动游戏",
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

        var dialogTask = dialog.ShowLocalizedAsync();

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

            var result = await _launchService.LaunchAsync(
                instance,
                account,
                progress,
                cancellation.Token);

            completed = true;
            if (!dialogClosed)
            {
                dialog.Hide();
            }

            await dialogTask;

            await AppServices.Music.HandleGameStartedAsync();

            if (AppServices.Settings.MinimizeOnLaunch)
            {
                if (App.MainWindow?.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.Minimize();
                }
            }
            else
            {
                await ShowMessageAsync(
                    "游戏已启动",
                    $"{instance.Id}\n进程 ID：{result.ProcessId}");
            }
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
            await ShowMessageAsync("启动失败", GetErrorMessage(exception));
        }
        finally
        {
            cancellation.Dispose();
            _launchCancellation = null;
            _isLaunching = false;
            LaunchButton.IsEnabled = true;
        }
    }

    private Task OnDeviceCodeReceivedAsync(DeviceCodeResult deviceCode)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_deviceCodeBox is not null)
            {
                _deviceCodeBox.Text = deviceCode.UserCode;
            }

            if (_openLoginPageLink is not null)
            {
                _openLoginPageLink.NavigateUri = new Uri(deviceCode.VerificationUrl);
                _openLoginPageLink.IsEnabled = true;
            }

            if (_deviceCodeStatus is not null)
            {
                _deviceCodeStatus.Text =
                    $"设备代码已复制到剪贴板，有效期至 {deviceCode.ExpiresOn.LocalDateTime:HH:mm}。" +
                    $"\n登录地址：{deviceCode.VerificationUrl}" +
                    "\n在浏览器中完成登录后，此窗口会自动关闭。";
            }

            try
            {
                var package = new DataPackage();
                package.SetText(deviceCode.UserCode);
                Clipboard.SetContent(package);
            }
            catch
            {
                // Clipboard access can be temporarily unavailable.
            }
        });

        return Task.CompletedTask;
    }

    private (ContentDialog Dialog, TextBox CodeBox, TextBlock Status, HyperlinkButton Link) CreateLoginDialog()
    {
        var codeBox = new TextBox
        {
            IsReadOnly = true,
            PlaceholderText = "正在获取设备代码…",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 26,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var openLink = new HyperlinkButton
        {
            Content = "打开 Microsoft 登录页面",
            IsEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var status = new TextBlock
        {
            Text = "正在向 Microsoft 请求设备代码，请稍候…",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
        };

        var content = new StackPanel
        {
            Spacing = 12,
            MinWidth = 420,
        };

        content.Children.Add(new TextBlock
        {
            Text = "1. 点击下方按钮打开 Microsoft 登录页面。",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(new TextBlock
        {
            Text = "2. 在页面中输入下面的设备代码并完成登录。",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(codeBox);
        content.Children.Add(openLink);
        content.Children.Add(status);

        var dialog = new ContentDialog
        {
            Title = "登录 Microsoft 账号",
            Content = content,
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        return (dialog, codeBox, status, openLink);
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
}
