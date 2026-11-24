using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace LiteMCLauncher.Pages;

public sealed partial class OnlineMusicSearchPage : Page
{
    public OnlineMusicService Online => AppServices.OnlineMusic;

    public OnlineMusicSearchPage()
    {
        InitializeComponent();
        Loaded += OnlineMusicSearchPage_Loaded;
    }

    private async void OnlineMusicSearchPage_Loaded(object sender, RoutedEventArgs e)
    {
        await Online.InitializeAsync();
        CookieBox.Password = Online.Cookie;
        EmptyResultsText.Visibility = Online.Results.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(OnlineMusicPage));
        }
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
            await Online.PlayAsync(song, Online.Results);
        }
    }

    private async void ResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is OnlineSong song)
        {
            await Online.PlayAsync(song, Online.Results);
        }
    }

    private async void SearchHistory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Content: string keyword })
        {
            SearchBox.Text = keyword;
            await Online.SearchAsync(keyword);
        }
    }

    private async void SaveCookie_Click(object sender, RoutedEventArgs e)
    {
        await Online.SaveCookieAsync(CookieBox.Password);
    }
}
