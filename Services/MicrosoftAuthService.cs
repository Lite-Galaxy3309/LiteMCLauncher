using System.Text.Json;
using CmlLib.Core.Auth;
using CmlLib.Core.Auth.Microsoft;
using CmlLib.Core.Auth.Microsoft.Sessions;
using LiteMCLauncher.Models;
using Microsoft.Identity.Client;
using XboxAuthNet.Game;
using XboxAuthNet.Game.Accounts;
using XboxAuthNet.Game.Msal;
using XboxAuthNet.Game.Msal.OAuth;
using XboxAuthNet.Game.OAuth;
using XboxAuthNet.OAuth;

namespace LiteMCLauncher.Services;

/// <summary>
/// Microsoft/offline account management built on CmlLib.Core.Auth.Microsoft.
/// Accounts are persisted in .minecraft/launcher_accounts.json. Microsoft
/// accounts keep their access token and refresh token; at startup the selected
/// account is refreshed silently through MSAL, without opening a browser.
/// </summary>
public sealed class MicrosoftAuthService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lazy<Task<IPublicClientApplication>> _msalApplication;
    private readonly JELoginHandler _loginHandler;
    private readonly AccountStore _accountStore = new();
    private readonly List<AccountModel> _accounts = [];

    private Task? _initializeTask;
    private AccountModel? _currentAccount;
    private string? _selectedAccountId;
    private byte[]? _lastMsalCacheBytes;

    public MicrosoftAuthService()
    {
        AppPaths.EnsureDirectories();

        _msalApplication = new Lazy<Task<IPublicClientApplication>>(BuildMsalApplicationAsync);

        _loginHandler = new JELoginHandlerBuilder()
            .WithAccountManager(AppPaths.AccountsFile)
            .Build();
    }

    private static string MsalCachePath =>
        Path.Combine(AppPaths.DataDirectory, "msal_cache.bin");

    private Task<IPublicClientApplication> BuildMsalApplicationAsync()
    {
        var msalApp = MsalClientHelper.BuildApplication(MicrosoftAuthOptions.ClientId);
        var cachePath = MsalCachePath;

        try
        {
            if (File.Exists(cachePath))
            {
                _lastMsalCacheBytes = File.ReadAllBytes(cachePath);
            }
        }
        catch
        {
        }

        msalApp.UserTokenCache.SetBeforeAccessAsync(args =>
        {
            try
            {
                if (File.Exists(cachePath))
                {
                    var bytes = File.ReadAllBytes(cachePath);
                    args.TokenCache.DeserializeMsalV3(bytes);
                    _lastMsalCacheBytes = bytes;
                }
            }
            catch
            {
                // A stale cache should not prevent interactive login.
            }

            return Task.CompletedTask;
        });

        msalApp.UserTokenCache.SetAfterAccessAsync(args =>
        {
            try
            {
                if (args.HasStateChanged)
                {
                    var bytes = args.TokenCache.SerializeMsalV3();
                    _lastMsalCacheBytes = bytes;
                    File.WriteAllBytes(cachePath, bytes);
                }
            }
            catch
            {
                // Cache persistence is best-effort.
            }

            return Task.CompletedTask;
        });

        return Task.FromResult(msalApp);
    }

    public AccountModel? CurrentAccount => _currentAccount;

    public IReadOnlyList<AccountModel> Accounts => _accounts.ToArray();

    public string? SelectedAccountId => _selectedAccountId;

    public event EventHandler<AccountModel?>? AccountChanged;

    public event EventHandler? AccountsChanged;

    /// <summary>
    /// Loads launcher_accounts.json, imports existing CmlLib accounts, and
    /// silently refreshes the selected Microsoft account.
    /// </summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initializeTask is null)
        {
            lock (_accounts)
            {
                _initializeTask ??= InitializeCoreAsync(cancellationToken);
            }
        }

        return _initializeTask;
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var data = await _accountStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            _accounts.Clear();
            foreach (var account in data.Accounts)
            {
                if (string.IsNullOrWhiteSpace(account.Username) ||
                    _accounts.Any(existing => string.Equals(existing.Id, account.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                account.LastError = null;
                _accounts.Add(account);
            }

            if (!data.FileExisted)
            {
                // First run of the new multi-account store: migrate accounts
                // previously persisted by CmlLib and the legacy offline name.
                ImportCmlLibAccounts();
                ImportLegacyOfflineAccount();
            }

            _selectedAccountId = data.SelectedAccountId;
            var selected = FindAccount(_selectedAccountId)
                           ?? _accounts.FirstOrDefault(account => account.Type == AccountType.Microsoft)
                           ?? _accounts.FirstOrDefault();

            await ActivateCoreAsync(selected, allowSilentRefresh: true, cancellationToken).ConfigureAwait(false);
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        RaiseAccountsChanged();
        AccountChanged?.Invoke(this, _currentAccount);
    }

    /// <summary>
    /// Signs into a new Microsoft account through the device code flow and
    /// stores the resulting access/refresh tokens.
    /// </summary>
    public async Task<AccountModel> SignInWithDeviceCodeAsync(
        Func<DeviceCodeResult, Task> onDeviceCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onDeviceCode);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var msalApp = await _msalApplication.Value.WaitAsync(cancellationToken).ConfigureAwait(false);

            var authenticator = _loginHandler.CreateAuthenticatorWithNewAccount(cancellationToken);
            authenticator.AddMsalOAuth(msalApp, msal => msal.DeviceCode(onDeviceCode));
            authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
            authenticator.AddJEAuthenticator();

            var session = await authenticator.ExecuteForLauncherAsync().ConfigureAwait(false);
            var account = UpsertMicrosoftAccount(session);

            await CaptureRefreshTokenAsync(account, msalApp, cancellationToken).ConfigureAwait(false);

            account.LastLoginAt = DateTimeOffset.UtcNow;
            account.LastError = null;
            _currentAccount = account;
            _selectedAccountId = account.Id;

            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        RaiseAccountsChanged();
        AccountChanged?.Invoke(this, _currentAccount);
        return _currentAccount!;
    }

    /// <summary>
    /// Adds or updates a local offline account. Offline accounts are stored in
    /// the same multi-account file as Microsoft accounts.
    /// </summary>
    public async Task<AccountModel> SignInOfflineAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var incoming = AccountModel.CreateOffline(username);
            var account = _accounts.FirstOrDefault(existing =>
                existing.Type == AccountType.Offline &&
                (string.Equals(existing.Id, incoming.Id, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(existing.Uuid, incoming.Uuid, StringComparison.OrdinalIgnoreCase)));

            if (account is null)
            {
                account = incoming;
                _accounts.Add(account);
            }
            else
            {
                account.Username = incoming.Username;
                account.Uuid = incoming.Uuid;
                account.AccessToken = string.Empty;
                account.RefreshToken = null;
                account.Xuid = null;
            }

            account.LastLoginAt = DateTimeOffset.UtcNow;
            account.LastError = null;
            _currentAccount = account;
            _selectedAccountId = account.Id;

            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        RaiseAccountsChanged();
        AccountChanged?.Invoke(this, _currentAccount);
        return _currentAccount!;
    }

    /// <summary>
    /// Switches to a stored account. Microsoft accounts are refreshed silently.
    /// Returns false when the stored Microsoft session cannot be refreshed and
    /// the user must sign in again.
    /// </summary>
    public async Task<bool> SelectAccountAsync(
        string accountId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        var account = FindAccount(accountId);
        if (account is null)
        {
            _gate.Release();
            return false;
        }

        try
        {
            var ready = await ActivateCoreAsync(account, allowSilentRefresh: true, cancellationToken)
                .ConfigureAwait(false);
            _selectedAccountId = account.Id;
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
            RaiseAccountsChanged();
            AccountChanged?.Invoke(this, _currentAccount);
            return ready;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Ensures the selected account has a usable access token before launching.
    /// </summary>
    public async Task<bool> EnsureAccountReadyAsync(
        string accountId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        var account = FindAccount(accountId);
        if (account is null)
        {
            _gate.Release();
            return false;
        }

        try
        {
            if (account.IsOffline)
            {
                return true;
            }

            if (account.IsSessionFresh && account.LastError is null)
            {
                return true;
            }

            var ready = await ActivateCoreAsync(account, allowSilentRefresh: true, cancellationToken)
                .ConfigureAwait(false);
            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
            RaiseAccountsChanged();
            AccountChanged?.Invoke(this, _currentAccount);
            return ready;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAccountAsync(
        string accountId,
        CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var account = FindAccount(accountId);
            if (account is null)
            {
                return;
            }

            _accounts.Remove(account);
            if (string.Equals(_selectedAccountId, account.Id, StringComparison.OrdinalIgnoreCase) ||
                ReferenceEquals(_currentAccount, account))
            {
                var next = _accounts.FirstOrDefault();
                _selectedAccountId = next?.Id;
                _currentAccount = next;
                if (next is not null)
                {
                    await ActivateCoreAsync(next, allowSilentRefresh: true, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        RaiseAccountsChanged();
        AccountChanged?.Invoke(this, _currentAccount);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (_currentAccount is not null)
        {
            await RemoveAccountAsync(_currentAccount.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> ActivateCoreAsync(
        AccountModel? account,
        bool allowSilentRefresh,
        CancellationToken cancellationToken)
    {
        if (account is null)
        {
            _currentAccount = null;
            _selectedAccountId = null;
            return false;
        }

        account.LastError = null;
        _selectedAccountId = account.Id;

        if (account.Type == AccountType.Offline)
        {
            account.LastLoginAt = DateTimeOffset.UtcNow;
            _currentAccount = account;
            return true;
        }

        if (!allowSilentRefresh)
        {
            _currentAccount = account;
            return account.IsSessionFresh;
        }

        try
        {
            await AuthenticateMicrosoftSilentlyAsync(account, cancellationToken).ConfigureAwait(false);
            account.LastLoginAt = DateTimeOffset.UtcNow;
            account.LastError = null;
            _currentAccount = account;
            return true;
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            account.LastError = exception.Message;
            _currentAccount = account;
            return account.IsSessionFresh;
        }
    }

    private async Task<MSession> AuthenticateMicrosoftSilentlyAsync(
        AccountModel account,
        CancellationToken cancellationToken)
    {
        var msalApp = await _msalApplication.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        var jeAccount = FindCmlLibAccount(account);

        if (jeAccount is null)
        {
            jeAccount = (JEGameAccount)_loginHandler.AccountManager.NewAccount();

            if (!string.IsNullOrWhiteSpace(account.MsalHomeAccountId))
            {
                MsalHomeAccountIdSource.Default.Set(jeAccount.SessionStorage, account.MsalHomeAccountId);
            }

            if (!string.IsNullOrWhiteSpace(account.Username))
            {
                MicrosoftOAuthLoginHintSource.Default.Set(jeAccount.SessionStorage, account.Username);
            }
        }
        else
        {
            account.MsalHomeAccountId ??= MsalHomeAccountIdSource.Default.Get(jeAccount.SessionStorage);
        }

        if (string.IsNullOrWhiteSpace(account.MsalHomeAccountId))
        {
            var msalAccount = (await msalApp.GetAccountsAsync().ConfigureAwait(false))
                .FirstOrDefault(candidate => string.Equals(
                    candidate.Username,
                    account.Username,
                    StringComparison.OrdinalIgnoreCase));

            if (msalAccount is not null)
            {
                account.MsalHomeAccountId = msalAccount.HomeAccountId.Identifier;
                MsalHomeAccountIdSource.Default.Set(jeAccount.SessionStorage, account.MsalHomeAccountId);
            }
        }

        Exception? refreshTokenError = null;

        if (!string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            try
            {
                var refreshedSession = await AuthenticateWithStoredRefreshTokenAsync(
                        account,
                        jeAccount,
                        cancellationToken)
                    .ConfigureAwait(false);

                await CaptureRefreshTokenAsync(account, msalApp, cancellationToken).ConfigureAwait(false);
                return refreshedSession;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                refreshTokenError = exception;
            }
        }

        try
        {
            var authenticator = _loginHandler.CreateAuthenticator(jeAccount, cancellationToken);
            authenticator.AddMsalOAuth(msalApp, msal => msal.Silent());
            authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
            authenticator.AddJEAuthenticator();

            var session = await authenticator.ExecuteForLauncherAsync().ConfigureAwait(false);
            UpdateFromSession(account, session, jeAccount);
            await CaptureRefreshTokenAsync(account, msalApp, cancellationToken).ConfigureAwait(false);
            return session;
        }
        catch (Exception exception) when (refreshTokenError is not null && exception is not OperationCanceledException)
        {
            throw new AggregateException(
                "Microsoft 登录刷新失败。",
                refreshTokenError,
                exception);
        }
    }

    /// <summary>
    /// Refreshes the Microsoft OAuth token directly from the stored refresh
    /// token, then runs the normal Xbox/Minecraft authentication chain. This
    /// path does not depend on MSAL's cache and never opens a browser.
    /// </summary>
    private async Task<MSession> AuthenticateWithStoredRefreshTokenAsync(
        AccountModel account,
        JEGameAccount jeAccount,
        CancellationToken cancellationToken)
    {
        var previousOAuth = MicrosoftOAuthSessionSource.Default.Get(jeAccount.SessionStorage);
        var seededOAuth = new MicrosoftOAuthResponse
        {
            RefreshToken = account.RefreshToken,
        };

        MicrosoftOAuthSessionSource.Default.Set(jeAccount.SessionStorage, seededOAuth);

        try
        {
            var authenticator = _loginHandler.CreateAuthenticator(jeAccount, cancellationToken);
            authenticator.AddForceMicrosoftOAuth(
                JELoginHandler.DefaultMicrosoftOAuthClientInfo,
                oauth => oauth.Silent());
            authenticator.AddXboxAuthForJE(xbox => xbox.Basic());
            authenticator.AddJEAuthenticator();

            var session = await authenticator.ExecuteForLauncherAsync().ConfigureAwait(false);
            var refreshedOAuth = MicrosoftOAuthSessionSource.Default.Get(jeAccount.SessionStorage);
            if (!string.IsNullOrWhiteSpace(refreshedOAuth?.RefreshToken))
            {
                account.RefreshToken = refreshedOAuth.RefreshToken;
            }

            UpdateFromSession(account, session, jeAccount);
            return session;
        }
        catch
        {
            if (previousOAuth is not null)
            {
                MicrosoftOAuthSessionSource.Default.Set(jeAccount.SessionStorage, previousOAuth);
            }

            throw;
        }
    }

    private AccountModel UpsertMicrosoftAccount(MSession session)
    {
        var profileUuid = session.UUID ?? string.Empty;
        var username = string.IsNullOrWhiteSpace(session.Username)
            ? "Minecraft 玩家"
            : session.Username;

        var account = _accounts.FirstOrDefault(existing =>
            existing.Type == AccountType.Microsoft &&
            ((!string.IsNullOrWhiteSpace(profileUuid) &&
              string.Equals(existing.Uuid, profileUuid, StringComparison.OrdinalIgnoreCase)) ||
             (string.IsNullOrWhiteSpace(existing.Uuid) &&
              string.Equals(existing.Username, username, StringComparison.OrdinalIgnoreCase))));

        if (account is null)
        {
            account = new AccountModel
            {
                Id = string.IsNullOrWhiteSpace(profileUuid)
                    ? $"msa:{Guid.NewGuid():N}"
                    : $"msa:{profileUuid.ToLowerInvariant()}",
                Type = AccountType.Microsoft,
            };
            _accounts.Add(account);
        }

        account.Type = AccountType.Microsoft;
        account.Username = username;
        account.Uuid = profileUuid;
        account.AccessToken = session.AccessToken ?? string.Empty;
        account.Xuid = session.Xuid;
        account.LastLoginAt = DateTimeOffset.UtcNow;

        var jeAccount = FindCmlLibAccount(account);
        if (jeAccount is not null)
        {
            account.MsalHomeAccountId ??= MsalHomeAccountIdSource.Default.Get(jeAccount.SessionStorage);
            account.AccessTokenExpiresAt = ToDateTimeOffset(jeAccount.Token?.ExpiresOn);
        }

        return account;
    }

    private void UpdateFromSession(AccountModel account, MSession session, JEGameAccount jeAccount)
    {
        account.Username = string.IsNullOrWhiteSpace(session.Username)
            ? account.Username
            : session.Username;
        account.Uuid = string.IsNullOrWhiteSpace(session.UUID)
            ? account.Uuid
            : session.UUID;
        account.AccessToken = session.AccessToken ?? account.AccessToken;
        account.Xuid = session.Xuid ?? account.Xuid;
        account.AccessTokenExpiresAt = ToDateTimeOffset(jeAccount.Token?.ExpiresOn);
        account.MsalHomeAccountId ??= MsalHomeAccountIdSource.Default.Get(jeAccount.SessionStorage);
    }

    private void ImportCmlLibAccounts()
    {
        try
        {
            foreach (var jeAccount in _loginHandler.AccountManager.GetAccounts().OfType<JEGameAccount>())
            {
                var profile = jeAccount.Profile;
                if (profile is null || string.IsNullOrWhiteSpace(profile.UUID))
                {
                    continue;
                }

                var account = _accounts.FirstOrDefault(existing =>
                    existing.Type == AccountType.Microsoft &&
                    string.Equals(existing.Uuid, profile.UUID, StringComparison.OrdinalIgnoreCase));

                if (account is null)
                {
                    account = new AccountModel
                    {
                        Id = $"msa:{profile.UUID.ToLowerInvariant()}",
                        Type = AccountType.Microsoft,
                        Uuid = profile.UUID,
                    };
                    _accounts.Add(account);
                }

                account.Username = profile.Username ?? account.Username;
                account.Uuid = profile.UUID;
                account.AccessToken = jeAccount.Token?.AccessToken ?? account.AccessToken;
                account.AccessTokenExpiresAt = ToDateTimeOffset(jeAccount.Token?.ExpiresOn);
                account.MsalHomeAccountId ??= MsalHomeAccountIdSource.Default.Get(jeAccount.SessionStorage);
                account.LastLoginAt = jeAccount.LastAccess == default
                    ? account.LastLoginAt
                    : jeAccount.LastAccess;
                account.LastError = null;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Existing CmlLib account file is optional.
        }
    }

    private void ImportLegacyOfflineAccount()
    {
        if (_accounts.Count > 0)
        {
            return;
        }

        try
        {
            var legacyUsername = AppServices.Settings.Current.OfflineUsername;
            if (!string.IsNullOrWhiteSpace(legacyUsername))
            {
                _accounts.Add(AccountModel.CreateOffline(legacyUsername));
            }
        }
        catch
        {
            // AppServices.Settings may not be initialized during very early startup.
        }
    }

    private JEGameAccount? FindCmlLibAccount(AccountModel account)
    {
        try
        {
            return _loginHandler.AccountManager.GetAccounts()
                .OfType<JEGameAccount>()
                .FirstOrDefault(candidate =>
                    (!string.IsNullOrWhiteSpace(account.Uuid) &&
                     string.Equals(candidate.Profile?.UUID, account.Uuid, StringComparison.OrdinalIgnoreCase)) ||
                    string.Equals(candidate.Profile?.Username, account.Username, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private AccountModel? FindAccount(string? accountId)
    {
        return string.IsNullOrWhiteSpace(accountId)
            ? null
            : _accounts.FirstOrDefault(account =>
                string.Equals(account.Id, accountId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task CaptureRefreshTokenAsync(
        AccountModel account,
        IPublicClientApplication msalApp,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var msalAccounts = await msalApp.GetAccountsAsync().ConfigureAwait(false);
            var msalAccount = msalAccounts.FirstOrDefault(candidate =>
                                  string.Equals(candidate.Username, account.Username, StringComparison.OrdinalIgnoreCase))
                              ?? msalAccounts.FirstOrDefault(candidate =>
                                  string.Equals(
                                      candidate.HomeAccountId?.Identifier,
                                      account.MsalHomeAccountId,
                                      StringComparison.OrdinalIgnoreCase));

            if (msalAccount is not null)
            {
                account.MsalHomeAccountId ??= msalAccount.HomeAccountId.Identifier;
            }

            var cacheBytes = _lastMsalCacheBytes;
            if (cacheBytes is null || cacheBytes.Length == 0)
            {
                try
                {
                    if (File.Exists(MsalCachePath))
                    {
                        cacheBytes = await File.ReadAllBytesAsync(MsalCachePath, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
                catch
                {
                    return;
                }
            }

            if (cacheBytes is null || cacheBytes.Length == 0)
            {
                return;
            }

            using var document = JsonDocument.Parse(cacheBytes);
            var root = document.RootElement;

            if (string.IsNullOrWhiteSpace(account.MsalHomeAccountId) &&
                root.TryGetProperty("Account", out var accountEntries))
            {
                foreach (var entry in accountEntries.EnumerateObject())
                {
                    var username = GetJsonString(entry.Value, "username");
                    if (string.Equals(username, account.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        account.MsalHomeAccountId = GetJsonString(entry.Value, "home_account_id")
                                                    ?? entry.Name.Split('-')[0];
                        break;
                    }
                }
            }

            if (!root.TryGetProperty("RefreshToken", out var refreshTokens))
            {
                return;
            }

            string? firstRefreshToken = null;

            foreach (var entry in refreshTokens.EnumerateObject())
            {
                var secret = GetJsonString(entry.Value, "secret");
                if (string.IsNullOrWhiteSpace(secret))
                {
                    continue;
                }

                firstRefreshToken ??= secret;
                var homeAccountId = GetJsonString(entry.Value, "home_account_id");

                if (!string.IsNullOrWhiteSpace(account.MsalHomeAccountId) &&
                    string.Equals(homeAccountId, account.MsalHomeAccountId, StringComparison.OrdinalIgnoreCase))
                {
                    account.RefreshToken = secret;
                    return;
                }
            }

            account.RefreshToken ??= firstRefreshToken;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The access token may still be valid even if the cache format changes.
        }
    }

    private async Task SaveCoreAsync(CancellationToken cancellationToken)
    {
        await _accountStore.SaveAsync(_accounts, _selectedAccountId, cancellationToken)
            .ConfigureAwait(false);
    }

    private void RaiseAccountsChanged()
    {
        AccountsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        if (value is null || value.Value == default)
        {
            return null;
        }

        var dateTime = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();

        return new DateTimeOffset(dateTime);
    }
}
