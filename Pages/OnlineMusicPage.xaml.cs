using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace LiteMCLauncher.Pages;

public sealed partial class OnlineMusicPage : Page
{
    public OnlineMusicService Online => AppServices.OnlineMusic;

    public OnlineMusicPage()
    {
        InitializeComponent();
        Loaded += OnlineMusicPage_Loaded;
    }

    private async void OnlineMusicPage_Loaded(object sender, RoutedEventArgs e)
    {
        await Online.InitializeAsync();
        CookieBox.Password = Online.Cookie;
    }

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        await SearchAsync();
    }

    private async void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            await SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        var keyword = SearchBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return;
        }

        await Online.SearchAsync(keyword);
        EmptyResultsText.Visibility = Online.Results.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void PlaySong_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: OnlineSong song })
        {
            await Online.PlayAsync(song);
        }
    }

    private async void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OnlineSong song)
        {
            await Online.PlayAsync(song);
        }
    }

    private async void HistoryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OnlineHistoryItem item)
        {
            await Online.PlayHistoryAsync(item);
        }
    }

    private async void SearchHistoryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string keyword)
        {
            SearchBox.Text = keyword;
            await Online.SearchAsync(keyword);
        }
    }

    private async void SaveCookie_Click(object sender, RoutedEventArgs e)
    {
        await Online.SaveCookieAsync(CookieBox.Password);
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        Online.TogglePlayPause();
    }
}
