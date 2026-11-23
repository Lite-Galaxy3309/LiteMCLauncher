using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Net.Http;
using Windows.Storage.Streams;

namespace LiteMCLauncher.Services;

/// <summary>
/// Loads a Minecraft player's head/skin avatar from public skin renderers,
/// with a local cache and a bundled default head as the offline fallback.
/// </summary>
public sealed class PlayerAvatarService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public async Task<ImageSource> GetAvatarAsync(
        Models.AccountModel? account,
        CancellationToken cancellationToken = default)
    {
        if (account is null || account.IsOffline || string.IsNullOrWhiteSpace(account.Uuid))
        {
            return await GetDefaultAvatarAsync(cancellationToken);
        }

        var cacheDirectory = Path.Combine(AppPaths.DataDirectory, "avatars");
        var cachePath = Path.Combine(cacheDirectory, $"{Sanitize(account.Uuid)}.png");

        try
        {
            Directory.CreateDirectory(cacheDirectory);

            if (File.Exists(cachePath) &&
                DateTimeOffset.Now - File.GetLastWriteTimeUtc(cachePath) < TimeSpan.FromDays(7))
            {
                var cached = await File.ReadAllBytesAsync(cachePath, cancellationToken);
                if (IsPng(cached))
                {
                    return await CreateBitmapAsync(cached);
                }
            }

            var uuid = account.Uuid.Replace("-", string.Empty);
            var formattedUuid = Models.AccountModel.FormatUuid(account.Uuid);
            var urls = new[]
            {
                $"https://crafatar.com/avatars/{formattedUuid}?size=128&overlay",
                $"https://mc-heads.net/avatar/{uuid}/128",
                $"https://minotar.net/helm/{uuid}/128.png",
            };

            foreach (var url in urls)
            {
                try
                {
                    using var response = await HttpClient.GetAsync(url, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    if (!IsPng(bytes))
                    {
                        continue;
                    }

                    await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
                    return await CreateBitmapAsync(bytes);
                }
                catch (Exception exception) when (
                    exception is HttpRequestException or TaskCanceledException or IOException)
                {
                    // Try the next renderer, then the bundled fallback.
                }
            }

            if (File.Exists(cachePath))
            {
                var stale = await File.ReadAllBytesAsync(cachePath, cancellationToken);
                if (IsPng(stale))
                {
                    return await CreateBitmapAsync(stale);
                }
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or HttpRequestException)
        {
            // Fall through to the default head.
        }

        return await GetDefaultAvatarAsync(cancellationToken);
    }

    public async Task<ImageSource> GetDefaultAvatarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultHead.png");
            if (File.Exists(path))
            {
                return await CreateBitmapAsync(await File.ReadAllBytesAsync(path, cancellationToken));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        return new BitmapImage(new Uri("ms-appx:///Assets/DefaultHead.png"));
    }

    private static async Task<BitmapImage> CreateBitmapAsync(byte[] bytes)
    {
        var stream = new InMemoryRandomAccessStream();
        var writer = new DataWriter(stream);
        writer.WriteBytes(bytes);
        await writer.StoreAsync();
        await writer.FlushAsync();
        writer.DetachStream();
        stream.Seek(0);

        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private static bool IsPng(byte[] bytes)
    {
        return bytes.Length > PngSignature.Length &&
               bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);
    }

    private static string Sanitize(string value)
    {
        return new string(value
            .Where(character => char.IsLetterOrDigit(character))
            .ToArray());
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) LiteMCLauncher/0.1.0");
        return client;
    }
}
