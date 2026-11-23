using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

namespace LiteMCLauncher.Pages;

public sealed partial class NewsDetailPage : Page
{
    private NewsItem? _item;

    public NewsDetailPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not NewsItem item)
        {
            return;
        }

        _item = item;
        HeaderTitle.Text = item.Title;
        HeaderMeta.Text = $"{item.Source} · {item.Category} · {item.DateDisplay}";

        LoadingRing.IsActive = true;
        ArticlePanel.Children.Clear();

        try
        {
            var html = await AppServices.News.GetArticleHtmlAsync(item);
            var baseUrl = string.IsNullOrWhiteSpace(item.ContentPath)
                ? "https://www.minecraft.net"
                : "https://launchercontent.mojang.com";
            var blocks = ArticleContentParser.Parse(html ?? string.Empty, baseUrl);

            AddHeaderImage(item.ImageUrl);

            if (blocks.Count == 0)
            {
                AddParagraph(string.IsNullOrWhiteSpace(item.Summary)
                    ? "这篇文章暂时没有可显示的正文。"
                    : item.Summary);
            }
            else
            {
                foreach (var block in blocks)
                {
                    switch (block.Kind)
                    {
                        case ArticleBlockKind.Heading:
                            AddHeading(block.Text);
                            break;
                        case ArticleBlockKind.Bullet:
                            AddBullet(block.Text);
                            break;
                        case ArticleBlockKind.Image:
                            AddImage(block.ImageUrl);
                            break;
                        default:
                            AddParagraph(block.Text);
                            break;
                    }
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AddParagraph($"文章加载失败：{exception.Message}");
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }

    private void AddHeaderImage(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            AddImage(url);
        }
    }

    private void AddHeading(string text)
    {
        ArticlePanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 22,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0),
        });
    }

    private void AddParagraph(string text)
    {
        ArticlePanel.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            LineHeight = 24,
        });
    }

    private void AddBullet(string text)
    {
        ArticlePanel.Children.Add(new TextBlock
        {
            Text = "•  " + text,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            LineHeight = 24,
            Margin = new Thickness(12, 0, 0, 0),
        });
    }

    private void AddImage(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        try
        {
            var image = new Image
            {
                Source = new BitmapImage(uri),
                MaxWidth = 860,
                MaxHeight = 460,
                HorizontalAlignment = HorizontalAlignment.Left,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 6, 0, 6),
            };
            ArticlePanel.Children.Add(image);
        }
        catch
        {
            // A broken image should not hide the article text.
        }
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void BrowserButton_Click(object sender, RoutedEventArgs e)
    {
        if (_item?.LinkUri is not null)
        {
            await Windows.System.Launcher.LaunchUriAsync(_item.LinkUri);
        }
    }
}
