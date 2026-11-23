using System.Text.Json;
using LiteMCLauncher.Models;

namespace LiteMCLauncher.Services;

/// <summary>
/// Reads Mojang's public launcher content feeds: general Minecraft news and
/// Java Edition patch notes.
/// </summary>
public sealed class NewsService
{
    private const string ContentBase = "https://launchercontent.mojang.com";
    private const string NewsUrl = ContentBase + "/v2/news.json";
    private const string PatchNotesUrl = ContentBase + "/v2/javaPatchNotes.json";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<NewsItem>> GetNewsAsync(
        CancellationToken cancellationToken = default)
    {
        var newsTask = GetGeneralNewsAsync(cancellationToken);
        var patchNotesTask = GetPatchNotesAsync(cancellationToken);
        await Task.WhenAll(newsTask, patchNotesTask);

        return newsTask.Result
            .Concat(patchNotesTask.Result)
            .OrderByDescending(item => item.Date)
            .Take(40)
            .ToList();
    }

    public async Task<string?> GetArticleHtmlAsync(
        NewsItem item,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(item.HtmlBody))
        {
            return item.HtmlBody;
        }

        if (string.IsNullOrWhiteSpace(item.ContentPath))
        {
            if (item.LinkUri is null)
            {
                return null;
            }

            try
            {
                var page = await HttpClient.GetStringAsync(item.LinkUri, cancellationToken);
                return ExtractArticleHtml(page);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException)
            {
                return null;
            }
        }

        var url = ContentBase + "/v2/" + item.ContentPath.TrimStart('/');
        var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var content = await JsonSerializer.DeserializeAsync<ArticleContent>(
            stream, JsonOptions, cancellationToken);
        return content?.Body;
    }

    private static string ExtractArticleHtml(string page)
    {
        page = System.Text.RegularExpressions.Regex.Replace(
            page,
            @"<(script|style|nav|header|footer|aside)\b[^>]*>.*?</\1>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (var pattern in new[]
                 {
                     @"<article\b[^>]*>(.*?)</article>",
                     @"<main\b[^>]*>(.*?)</main>",
                 })
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                page,
                pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Singleline);
            if (match.Success && match.Groups[1].Value.Length > 200)
            {
                return match.Groups[1].Value;
            }
        }

        return page;
    }

    private static async Task<IReadOnlyList<NewsItem>> GetGeneralNewsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await HttpClient.GetAsync(NewsUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var feed = await JsonSerializer.DeserializeAsync<NewsFeed>(
                stream, JsonOptions, cancellationToken);
            if (feed?.Entries is null)
            {
                return [];
            }

            return feed.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Title))
                .Select(entry => new NewsItem
                {
                    Title = entry.Title!,
                    Category = entry.Category ?? "Minecraft",
                    Source = "Mojang 新闻",
                    Summary = entry.Text ?? string.Empty,
                    ImageUrl = ToAbsoluteImageUrl(
                        entry.NewsPageImage?.Url ?? entry.PlayPageImage?.Url),
                    Link = entry.ReadMoreLink ?? string.Empty,
                    HtmlBody = entry.ArticleBody ?? string.Empty,
                    Date = ParseDate(entry.Date),
                })
                .ToList();
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            return [];
        }
    }

    private static async Task<IReadOnlyList<NewsItem>> GetPatchNotesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await HttpClient.GetAsync(PatchNotesUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var feed = await JsonSerializer.DeserializeAsync<PatchNotesFeed>(
                stream, JsonOptions, cancellationToken);
            if (feed?.Entries is null)
            {
                return [];
            }

            return feed.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Title))
                .Select(entry => new NewsItem
                {
                    Title = entry.Title!,
                    Category = entry.Type == "snapshot" ? "快照更新" : "Java 版更新",
                    Source = "Mojang 更新",
                    Summary = entry.ShortText ?? string.Empty,
                    ImageUrl = ToAbsoluteImageUrl(entry.Image?.Url),
                    Link = "https://www.minecraft.net/en-us/articles",
                    ContentPath = entry.ContentPath ?? string.Empty,
                    Date = ParseDate(entry.Date),
                })
                .ToList();
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            return [];
        }
    }

    private static string ToAbsoluteImageUrl(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return string.Empty;
        }

        return relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? relativeUrl
            : ContentBase + relativeUrl;
    }

    private static DateTimeOffset ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var date) ? date : default;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("LiteMCLauncher/0.2.0");
        return client;
    }

    private sealed class NewsFeed
    {
        public List<NewsEntry>? Entries { get; set; }
    }

    private sealed class NewsEntry
    {
        public string? Title { get; set; }
        public string? Category { get; set; }
        public string? Text { get; set; }
        public string? Date { get; set; }
        public string? ReadMoreLink { get; set; }
        public string? ArticleBody { get; set; }
        public NewsImage? NewsPageImage { get; set; }
        public NewsImage? PlayPageImage { get; set; }
    }

    private sealed class PatchNotesFeed
    {
        public List<PatchNoteEntry>? Entries { get; set; }
    }

    private sealed class PatchNoteEntry
    {
        public string? Title { get; set; }
        public string? Type { get; set; }
        public string? Date { get; set; }
        public string? ShortText { get; set; }
        public string? ContentPath { get; set; }
        public NewsImage? Image { get; set; }
    }

    private sealed class ArticleContent
    {
        public string? Body { get; set; }
    }

    private sealed class NewsImage
    {
        public string? Url { get; set; }
    }
}
