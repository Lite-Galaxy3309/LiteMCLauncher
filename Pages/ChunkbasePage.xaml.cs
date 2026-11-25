using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LiteMCLauncher.Pages;

public sealed partial class ChunkbasePage : Page
{
    private const string SeedMapUrl = "https://www.chunkbase.com/apps/seed-map";

    public ChunkbasePage()
    {
        InitializeComponent();
        Loaded += ChunkbasePage_Loaded;
    }

    private async void ChunkbasePage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();
            if (WebView.CoreWebView2 is not null)
            {
                WebView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                WebView.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            }

            WebView.Source = new Uri(SeedMapUrl);
            _ = HideLoadingFallbackAsync();
        }
        catch
        {
            HideLoading();
        }
    }

    private async Task HideLoadingFallbackAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(12));
        HideLoading();
    }

    private void CoreWebView2_NavigationCompleted(
        Microsoft.Web.WebView2.Core.CoreWebView2 sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
    {
        HideLoading();
    }

    private void CoreWebView2_DOMContentLoaded(
        Microsoft.Web.WebView2.Core.CoreWebView2 sender,
        Microsoft.Web.WebView2.Core.CoreWebView2DOMContentLoadedEventArgs args)
    {
        HideLoading();
    }

    private void HideLoading()
    {
        LoadingRing.IsActive = false;
        LoadingRing.Visibility = Visibility.Collapsed;
    }

    private void WebView_NavigationCompleted(
        WebView2 sender,
        Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
    {
        HideLoading();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(ToolsPage));
        }
    }
}
