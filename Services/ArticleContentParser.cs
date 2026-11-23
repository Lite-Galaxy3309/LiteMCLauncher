using System.Net;
using System.Text.RegularExpressions;

namespace LiteMCLauncher.Services;

public enum ArticleBlockKind
{
    Paragraph,
    Heading,
    Bullet,
    Image,
}

public sealed class ArticleBlock
{
    public ArticleBlockKind Kind { get; init; }

    public string Text { get; init; } = string.Empty;

    public string ImageUrl { get; init; } = string.Empty;
}

/// <summary>
/// Converts the HTML fragments returned by Mojang's content API into simple
/// native blocks so articles can be rendered without a browser control.
/// </summary>
public static partial class ArticleContentParser
{
    public static IReadOnlyList<ArticleBlock> Parse(string html, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        html = ScriptStyleRegex().Replace(html, string.Empty);
        html = ImageRegex().Replace(html, match =>
        {
            var src = match.Groups[1].Value;
            return $"\n@@IMG:{src}@@\n";
        });
        html = HeadingRegex().Replace(html, match =>
            $"\n@@HEADING:{match.Groups[1].Value}@@\n");
        html = ListItemRegex().Replace(html, "\n@@BULLET:");
        html = ListItemEndRegex().Replace(html, "@@\n");
        html = BlockBreakRegex().Replace(html, "\n");
        html = BreakRegex().Replace(html, "\n");
        html = TagRegex().Replace(html, string.Empty);
        html = WebUtility.HtmlDecode(html);

        var blocks = new List<ArticleBlock>();
        foreach (var rawLine in html.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("@@IMG:", StringComparison.Ordinal) &&
                line.EndsWith("@@", StringComparison.Ordinal))
            {
                var url = line[6..^2].Trim();
                if (url.Length > 0)
                {
                    blocks.Add(new ArticleBlock
                    {
                        Kind = ArticleBlockKind.Image,
                        ImageUrl = ResolveUrl(url, baseUrl),
                    });
                }

                continue;
            }

            if (line.StartsWith("@@HEADING:", StringComparison.Ordinal) &&
                line.EndsWith("@@", StringComparison.Ordinal))
            {
                var text = line[10..^2].Trim();
                if (text.Length > 0)
                {
                    blocks.Add(new ArticleBlock
                    {
                        Kind = ArticleBlockKind.Heading,
                        Text = text,
                    });
                }

                continue;
            }

            if (line.StartsWith("@@BULLET:", StringComparison.Ordinal))
            {
                var text = line[9..].Trim();
                if (text.Length > 0)
                {
                    blocks.Add(new ArticleBlock
                    {
                        Kind = ArticleBlockKind.Bullet,
                        Text = text,
                    });
                }

                continue;
            }

            if (line is "@@" or "•")
            {
                continue;
            }

            blocks.Add(new ArticleBlock
            {
                Kind = ArticleBlockKind.Paragraph,
                Text = line,
            });
        }

        return blocks;
    }

    private static string ResolveUrl(string url, string baseUrl)
    {
        url = url.Trim();
        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        if (Uri.TryCreate(new Uri(baseUrl, UriKind.Absolute), url, out var resolved))
        {
            return resolved.ToString();
        }

        return url;
    }

    [GeneratedRegex(@"<(script|style|noscript)\b[^>]*>.*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"<img\b[^>]*?src\s*=\s*[""']([^""']+)[""'][^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex ImageRegex();

    [GeneratedRegex(@"<h[1-6]\b[^>]*>(.*?)</h[1-6]>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"</li\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItemEndRegex();

    [GeneratedRegex(@"</?(p|div|h[1-6]|ul|ol|blockquote|section)\b[^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreakRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();
}
