using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using LiteMCLauncher.Services;

namespace LiteMCLauncher.Models;

public enum AccountType
{
    Microsoft,
    Offline,
}

/// <summary>
/// A persisted launcher account. Microsoft accounts keep the current Minecraft
/// access token and the Microsoft refresh token so they can be refreshed
/// silently the next time the launcher starts.
/// </summary>
public sealed class AccountModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public AccountType Type { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Uuid { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public string? RefreshToken { get; set; }

    public string? Xuid { get; set; }

    /// <summary>
    /// MSAL home account id, used to select the matching account from the
    /// encrypted MSAL token cache without asking the user to sign in again.
    /// </summary>
    public string? MsalHomeAccountId { get; set; }

    public DateTimeOffset? AccessTokenExpiresAt { get; set; }

    public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore]
    public bool IsOffline => Type == AccountType.Offline;

    [JsonIgnore]
    public string AccountTypeDisplay => IsOffline
        ? (LauncherLanguageService.IsEnglish ? "Offline" : "离线")
        : (LauncherLanguageService.IsEnglish ? "Microsoft" : "微软");

    [JsonIgnore]
    public string DisplayName => Username;

    [JsonIgnore]
    public string FormattedUuid => FormatUuid(Uuid);

    [JsonIgnore]
    public string Initials
    {
        get
        {
            var name = Username?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                return "MC";
            }

            return (name.Length >= 2 ? name[..2] : name).ToUpperInvariant();
        }
    }

    [JsonIgnore]
    public bool IsSessionFresh =>
        !IsOffline &&
        !string.IsNullOrWhiteSpace(AccessToken) &&
        AccessTokenExpiresAt is { } expiresAt &&
        expiresAt > DateTimeOffset.UtcNow.AddMinutes(2);

    [JsonIgnore]
    public string? LastError { get; set; }

    public override string ToString() => $"{Username} ({AccountTypeDisplay})";

    public static AccountModel CreateOffline(string username)
    {
        var normalized = string.IsNullOrWhiteSpace(username) ? "Player" : username.Trim();
        var uuid = CreateOfflineUuid(normalized);
        return new AccountModel
        {
            Id = $"offline:{uuid}",
            Type = AccountType.Offline,
            Username = normalized,
            Uuid = uuid,
            AccessToken = string.Empty,
            LastLoginAt = DateTimeOffset.UtcNow,
        };
    }

    public static string CreateOfflineUuid(string username)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"OfflinePlayer:{username}"));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string FormatUuid(string? uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid))
        {
            return "未知";
        }

        var hex = uuid.Replace("-", string.Empty);
        if (hex.Length != 32)
        {
            return uuid;
        }

        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }
}
