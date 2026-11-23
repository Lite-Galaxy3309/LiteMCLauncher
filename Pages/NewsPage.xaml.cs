using System.Collections.ObjectModel;
using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteMCLauncher.Pages;

public sealed partial class NewsPage : Page
{
    private bool _hasLoaded;

    public ObservableCollection<NewsItem> Items { get; } = [];

    public NewsPage()
    {
        InitializeComponent();
        Loaded += NewsPage_Loaded;
    }

    private async void NewsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await LoadAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadAsync();
    }

    private void NewsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is NewsItem item)
        {
            Frame.Navigate(typeof(NewsDetailPage), item);
        }
    }

    private void ReadMore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: NewsItem item })
        {
            Frame.Navigate(typeof(NewsDetailPage), item);
        }
    }

    private async Task LoadAsync()
    {
        LoadingRing.IsActive = true;
        EmptyText.Visibility = Visibility.Visible;
        EmptyText.Text = "正在读取 Mojang 新闻…";

        try
        {
            var items = await AppServices.News.GetNewsAsync();
            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            if (Items.Count == 0)
            {
                EmptyText.Text = "暂时没有读取到新闻，请检查网络后点击“刷新新闻”。";
            }
            else
            {
                EmptyText.Visibility = Visibility.Collapsed;
                NewsList.ScrollIntoView(Items[0]);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            EmptyText.Visibility = Visibility.Visible;
            EmptyText.Text = $"新闻读取失败：{exception.Message}";
        }
        finally
        {
            LoadingRing.IsActive = false;
        }
    }
}
