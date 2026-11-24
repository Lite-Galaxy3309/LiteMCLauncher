using System.Collections.ObjectModel;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace LiteMCLauncher.ViewModels;

/// <summary>
/// Account and selected-instance state for the Home page.
/// </summary>
public sealed class HomeViewModel : ObservableObject, IDisposable
{
    private readonly MicrosoftAuthService _authService;
    private readonly MinecraftInstanceService _instanceService;
    private readonly LauncherState _launcherState;
    private readonly JavaService _javaService;
    private readonly LauncherSettingsService _settingsService;
    private readonly PlayerAvatarService _avatarService;
    private bool _isLoggingIn;
    private MinecraftInstance? _selectedInstance;
    private AccountModel? _selectedAccount;
    private bool _suppressAccountSelection;
    private ImageSource? _accountAvatar;
    private string? _avatarAccountId;
    private string _javaStatusText = "正在检测 Java 运行时…";
    private bool _isJavaReady;

    public HomeViewModel(
        MicrosoftAuthService authService,
        MinecraftInstanceService instanceService,
        LauncherState launcherState,
        JavaService javaService,
        LauncherSettingsService settingsService,
        PlayerAvatarService avatarService)
    {
        _authService = authService;
        _instanceService = instanceService;
        _launcherState = launcherState;
        _javaService = javaService;
        _settingsService = settingsService;
        _avatarService = avatarService;

        Music.PropertyChanged += OnMusicPropertyChanged;
        AppServices.OnlineMusic.PropertyChanged += OnOnlineMusicPropertyChanged;
        _isAttached = true;
    }

    private bool _isAttached;

    public void Attach()
    {
        if (_isAttached)
        {
            return;
        }

        Music.PropertyChanged += OnMusicPropertyChanged;
        AppServices.OnlineMusic.PropertyChanged += OnOnlineMusicPropertyChanged;
        _isAttached = true;
    }

    private void OnMusicPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RaiseOverlayProperties();
    }

    private void OnOnlineMusicPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RaiseOverlayProperties();
    }

    public void Dispose()
    {
        if (!_isAttached)
        {
            return;
        }

        Music.PropertyChanged -= OnMusicPropertyChanged;
        AppServices.OnlineMusic.PropertyChanged -= OnOnlineMusicPropertyChanged;
        _isAttached = false;
    }

    public ObservableCollection<MinecraftInstance> Instances { get; } = [];

    public ObservableCollection<AccountModel> Accounts { get; } = [];

    public MusicService Music => AppServices.Music;

    public OnlineMusicService OnlineMusic => AppServices.OnlineMusic;

    public bool IsOnlineOverlay =>
        OnlineMusic.HasCurrentSong && (!Music.IsOverlayVisible || OnlineMusic.IsPlaying);

    public bool OverlayVisible => IsOnlineOverlay || Music.IsOverlayVisible;

    public string OverlayTitle => IsOnlineOverlay ? OnlineMusic.CurrentTitle : Music.CurrentTitle;

    public string OverlayArtist => IsOnlineOverlay ? OnlineMusic.CurrentArtist : Music.CurrentArtist;

    public ImageSource? OverlayArt
    {
        get
        {
            if (IsOnlineOverlay &&
                Uri.TryCreate(OnlineMusic.CurrentCoverUrl, UriKind.Absolute, out var uri))
            {
                return new BitmapImage(uri);
            }

            return Music.CurrentAlbumArt;
        }
    }

    public string OverlayPlayPauseGlyph => IsOnlineOverlay
        ? OnlineMusic.PlayPauseGlyph
        : Music.PlayPauseGlyph;

    public string OverlayRepeatDisplay => Music.RepeatModeDisplay;

    public bool OverlayQueueEnabled => !IsOnlineOverlay;

    public void RaiseOverlayProperties()
    {
        OnPropertyChanged(nameof(IsOnlineOverlay));
        OnPropertyChanged(nameof(OverlayVisible));
        OnPropertyChanged(nameof(OverlayTitle));
        OnPropertyChanged(nameof(OverlayArtist));
        OnPropertyChanged(nameof(OverlayArt));
        OnPropertyChanged(nameof(OverlayPlayPauseGlyph));
        OnPropertyChanged(nameof(OverlayRepeatDisplay));
        OnPropertyChanged(nameof(OverlayQueueEnabled));
    }

    public async Task ToggleOverlayPlayPauseAsync()
    {
        if (IsOnlineOverlay)
        {
            OnlineMusic.TogglePlayPause();
        }
        else
        {
            await Music.TogglePlayPauseAsync();
        }
    }

    public async Task NextOverlayAsync()
    {
        if (!IsOnlineOverlay)
        {
            await Music.NextAsync();
        }
    }

    public async Task PreviousOverlayAsync()
    {
        if (!IsOnlineOverlay)
        {
            await Music.PreviousAsync();
        }
    }

    public void CycleOverlayRepeat()
    {
        if (!IsOnlineOverlay)
        {
            Music.CycleRepeatMode();
        }
    }

    private AccountModel? Account => _authService.CurrentAccount;

    public AccountModel? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (!SetProperty(ref _selectedAccount, value))
            {
                return;
            }

            if (!_suppressAccountSelection && value is not null)
            {
                _ = SwitchAccountAsync(value);
            }
        }
    }

    public bool IsLoggedIn => Account is not null;

    public bool IsLoggingIn
    {
        get => _isLoggingIn;
        private set
        {
            if (SetProperty(ref _isLoggingIn, value))
            {
                OnPropertyChanged(nameof(CanLogIn));
                OnPropertyChanged(nameof(LoginButtonText));
            }
        }
    }

    public bool CanLogIn => !IsLoggingIn;

    public string AccountTitle => Account?.Username ?? "未选择账号";

    public string AccountInitials => Account?.Initials ?? "LM";

    public ImageSource? AccountAvatar => _accountAvatar;

    public string AccountStatus => Account is null
        ? "未登录"
        : Account.IsOffline
            ? "离线模式"
            : Account.LastError is not null
                ? "登录已过期"
                : "已登录";

    public string AccountDetail => Account is null
        ? "添加 Microsoft 账号或离线账号后即可启动游戏。"
        : Account.LastError is not null
            ? "Microsoft 登录已失效，请重新登录该账号。"
            : Account.IsOffline
                ? $"离线 UUID · {Account.FormattedUuid}"
                : $"UUID · {Account.FormattedUuid}";

    public string LoginButtonText => IsLoggingIn
        ? "正在登录…"
        : Accounts.Count > 0
            ? "添加 Microsoft 账号"
            : "登录 Microsoft 账号";

    public string SessionSummary => Account is null
        ? "离线模式 · 未登录"
        : Account.IsOffline
            ? $"离线模式 · {Account.Username}"
            : $"已登录 · {Account.Username}";

    public string AccountStatusLine => Account is null
        ? "尚未添加账号"
        : Account.IsOffline
            ? $"离线模式 · {Account.Username}"
            : Account.LastError is not null
                ? $"微软账号需要重新登录 · {Account.Username}"
                : $"微软账号已登录 · {Account.Username}";

    public MinecraftInstance? SelectedInstance
    {
        get => _selectedInstance;
        set
        {
            if (!SetProperty(ref _selectedInstance, value))
            {
                return;
            }

            _launcherState.SelectedInstance = value;
            OnPropertyChanged(nameof(SelectedInstanceTitle));
            OnPropertyChanged(nameof(SelectedGameVersion));
            OnPropertyChanged(nameof(SelectedLoader));
            OnPropertyChanged(nameof(LaunchTargetTitle));
            OnPropertyChanged(nameof(LaunchTargetSummary));
            OnPropertyChanged(nameof(LaunchSummary));
            _ = RefreshJavaStatusAsync();
        }
    }

    public string SelectedInstanceTitle => SelectedInstance?.DisplayName ?? "未选择实例";

    public string SelectedGameVersion => SelectedInstance?.GameVersion ?? "—";

    public string SelectedLoader => SelectedInstance?.LoaderDisplay ?? "—";

    public string JavaStatusText => _javaStatusText;

    public bool IsJavaReady => _isJavaReady;

    public string MemoryStatusText
    {
        get
        {
            if (SelectedInstance is { } instance)
            {
                var settings = InstanceSettingsStore.Get(instance.Id);
                if (settings.Enabled)
                {
                    return $"已分配 {settings.MaximumRamMb} MB 内存";
                }
            }

            return $"已分配 {_settingsService.MaximumRamMb} MB 内存";
        }
    }

    public string LaunchTargetTitle => SelectedInstance?.DisplayName ?? "未选择实例";

    public string LaunchTargetSummary => SelectedInstance is null
        ? "请先在实例管理中选择一个实例"
        : $"{SelectedInstance.Summary} · {(Account?.Username ?? "未登录")}";

    public string LaunchSummary => SelectedInstance is null
        ? $"请选择实例 · {(Account?.Username ?? "未登录")}"
        : $"{SelectedInstance.Summary} · {(Account?.Username ?? "未登录")}";

    public async Task LoadAsync()
    {
        try
        {
            await _authService.InitializeAsync();
        }
        catch
        {
            // The account file is optional; the launcher still opens without it.
        }

        RefreshAccounts();

        IReadOnlyList<MinecraftInstance> loaded;
        try
        {
            loaded = await _instanceService.GetInstancesAsync();
        }
        catch
        {
            loaded = [];
        }

        Instances.Clear();
        foreach (var instance in loaded)
        {
            Instances.Add(instance);
        }

        var preferred = _launcherState.SelectedInstance is { } current
            ? Instances.FirstOrDefault(instance => instance.Id == current.Id)
            : null;

        SelectedInstance = preferred ?? Instances.FirstOrDefault();

        await RefreshJavaStatusAsync();

        OnPropertyChanged(nameof(MemoryStatusText));
        RefreshAccount();
    }

    public async Task RefreshJavaStatusAsync()
    {
        try
        {
            var instance = SelectedInstance;
            var instanceSettings = instance is null ? null : InstanceSettingsStore.Get(instance.Id);
            var useInstanceSettings = instanceSettings is { Enabled: true };
            var instanceJava = useInstanceSettings ? instanceSettings!.JavaPath : null;

            if (useInstanceSettings && string.IsNullOrWhiteSpace(instanceJava))
            {
                var java = instance is not null
                    ? await _javaService.GetPreferredRuntimeForGameAsync(instance.GameVersion)
                    : await _javaService.GetPreferredRuntimeAsync();
                _isJavaReady = java is not null;
                _javaStatusText = java is null
                    ? "未检测到实例可用的 Java 运行时"
                    : $"实例自动 Java {java.Version} 已就绪";
            }
            else if (!string.IsNullOrWhiteSpace(instanceJava))
            {
                _isJavaReady = File.Exists(instanceJava) || Directory.Exists(instanceJava);
                _javaStatusText = _isJavaReady
                    ? $"实例 Java：{GetJavaDisplayName(instanceJava)}"
                    : "实例指定的 Java 路径无效";
            }
            else
            {
                var configuredJava = _settingsService.JavaPath;
                if (!string.IsNullOrWhiteSpace(configuredJava))
                {
                    var javaPath = configuredJava.Trim().Trim('"');
                    _isJavaReady = File.Exists(javaPath) || Directory.Exists(javaPath);
                    _javaStatusText = _isJavaReady
                        ? "已使用设置中的 Java 路径"
                        : "设置中的 Java 路径无效";
                }
                else
                {
                    var java = instance is not null
                        ? await _javaService.GetPreferredRuntimeForGameAsync(instance.GameVersion)
                        : await _javaService.GetPreferredRuntimeAsync();
                    _isJavaReady = java is not null;
                    _javaStatusText = java is null
                        ? "未检测到 Java 运行时"
                        : $"Java {java.Version} 已就绪";
                }
            }
        }
        catch
        {
            _isJavaReady = false;
            _javaStatusText = "Java 运行时检测失败";
        }

        OnPropertyChanged(nameof(IsJavaReady));
        OnPropertyChanged(nameof(JavaStatusText));
    }

    public void BeginLogin()
    {
        IsLoggingIn = true;
    }

    public void EndLogin()
    {
        IsLoggingIn = false;
        RefreshAccounts();
    }

    public void RefreshAccounts()
    {
        var selectedId = _authService.CurrentAccount?.Id ?? _selectedAccount?.Id;

        _suppressAccountSelection = true;
        try
        {
            Accounts.Clear();
            foreach (var account in _authService.Accounts
                         .OrderByDescending(account => string.Equals(
                             account.Id,
                             selectedId,
                             StringComparison.OrdinalIgnoreCase))
                         .ThenByDescending(account => account.LastLoginAt)
                         .ThenBy(account => account.Username, StringComparer.OrdinalIgnoreCase))
            {
                Accounts.Add(account);
            }

            _selectedAccount = Accounts.FirstOrDefault(account =>
                                  string.Equals(account.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                              ?? Accounts.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedAccount));
        }
        finally
        {
            _suppressAccountSelection = false;
        }

        RefreshAccount();
    }

    public async Task RemoveSelectedAccountAsync()
    {
        if (SelectedAccount is not { } account)
        {
            return;
        }

        await _authService.RemoveAccountAsync(account.Id);
        RefreshAccounts();
    }

    private async Task SwitchAccountAsync(AccountModel account)
    {
        try
        {
            await _authService.SelectAccountAsync(account.Id);
        }
        catch
        {
            account.LastError = "切换账号失败，请重新登录。";
        }

        _suppressAccountSelection = true;
        try
        {
            _selectedAccount = Accounts.FirstOrDefault(candidate =>
                                  string.Equals(
                                      candidate.Id,
                                      _authService.CurrentAccount?.Id,
                                      StringComparison.OrdinalIgnoreCase))
                              ?? account;
            OnPropertyChanged(nameof(SelectedAccount));
        }
        finally
        {
            _suppressAccountSelection = false;
        }

        RefreshAccount();
    }

    public void RefreshAccount()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(AccountTitle));
        OnPropertyChanged(nameof(AccountInitials));
        OnPropertyChanged(nameof(AccountAvatar));
        OnPropertyChanged(nameof(AccountStatus));
        OnPropertyChanged(nameof(AccountDetail));
        OnPropertyChanged(nameof(LoginButtonText));
        OnPropertyChanged(nameof(SessionSummary));
        OnPropertyChanged(nameof(AccountStatusLine));
        OnPropertyChanged(nameof(LaunchTargetSummary));
        OnPropertyChanged(nameof(LaunchSummary));
        _ = LoadAccountAvatarAsync();
    }

    private async Task LoadAccountAvatarAsync()
    {
        var account = Account;
        var key = account?.Id ?? "default";

        if (string.Equals(_avatarAccountId, key, StringComparison.OrdinalIgnoreCase) &&
            _accountAvatar is not null)
        {
            return;
        }

        _avatarAccountId = key;
        try
        {
            var avatar = await _avatarService.GetAvatarAsync(account);
            if (!string.Equals(Account?.Id ?? "default", key, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _accountAvatar = avatar;
            OnPropertyChanged(nameof(AccountAvatar));
        }
        catch
        {
            // Keep the previous avatar if a renderer is unavailable.
        }
    }

    private static string GetJavaDisplayName(string javaPath)
    {
        var binDirectory = Path.GetDirectoryName(javaPath);
        var homeDirectory = binDirectory is null ? null : Path.GetDirectoryName(binDirectory);
        return homeDirectory is not null && Directory.Exists(homeDirectory)
            ? Path.GetFileName(homeDirectory)
            : Path.GetFileName(javaPath);
    }
}
