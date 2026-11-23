using System.Text.Json;
using System.Text.Json.Serialization;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Reads and writes .minecraft/launcher_accounts.json.
/// </summary>
public sealed class AccountStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath => AppPaths.LauncherAccountsFile;

    public async Task<AccountStoreData> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AccountStoreData { FileExisted = false };
            }

            var json = await File.ReadAllTextAsync(FilePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AccountStoreData { FileExisted = true };
            }

            // Older builds may have written a bare account array.
            if (json.TrimStart().StartsWith('['))
            {
                var legacyAccounts = JsonSerializer.Deserialize<List<AccountModel>>(json, SerializerOptions) ?? [];
                return new AccountStoreData { Accounts = legacyAccounts, FileExisted = true };
            }

            var data = JsonSerializer.Deserialize<AccountStoreData>(json, SerializerOptions)
                       ?? new AccountStoreData();
            data.FileExisted = true;
            return data;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AccountStoreData();
        }
    }

    public async Task SaveAsync(
        IReadOnlyList<AccountModel> accounts,
        string? selectedAccountId,
        CancellationToken cancellationToken = default)
    {
        var data = new AccountStoreData
        {
            SelectedAccountId = selectedAccountId,
            Accounts = accounts.ToList(),
        };

        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(data, SerializerOptions);
            var temporaryPath = FilePath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
            File.Move(temporaryPath, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Account persistence must not crash the launcher.
        }
    }
}

public sealed class AccountStoreData
{
    public string? SelectedAccountId { get; set; }

    public List<AccountModel> Accounts { get; set; } = [];

    [System.Text.Json.Serialization.JsonIgnore]
    public bool FileExisted { get; set; }
}
