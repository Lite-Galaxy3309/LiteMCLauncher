using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
    }

    private void OpenSearch_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(OnlineMusicSearchPage));
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        Online.TogglePlayPause();
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        await Online.PreviousAsync();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        await Online.NextAsync();
    }

    private void PlaybackMode_Click(object sender, RoutedEventArgs e)
    {
        Online.CyclePlaybackMode();
    }

    private async void OpenHistory_Click(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var historyList = new ListView
        {
            MaxHeight = 360,
            ItemsSource = Online.History,
            ItemTemplate = (DataTemplate)Resources["HistoryDialogItemTemplate"],
            IsItemClickEnabled = true,
            SelectionMode = ListViewSelectionMode.None,
        };
        historyList.ItemClick += async (_, args) =>
        {
            if (args.ClickedItem is OnlineHistoryItem item)
            {
                await Online.PlayHistoryAsync(item);
            }
        };

        var modeBox = new ComboBox
        {
            Header = "播放模式",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        modeBox.Items.Add(new ComboBoxItem { Content = "列表循环", Tag = "RepeatAll" });
        modeBox.Items.Add(new ComboBoxItem { Content = "单曲循环", Tag = "RepeatOne" });
        modeBox.Items.Add(new ComboBoxItem { Content = "随机播放", Tag = "Shuffle" });
        modeBox.SelectedIndex = Online.PlaybackMode switch
        {
            "RepeatOne" => 1,
            "Shuffle" => 2,
            _ => 0,
        };
        modeBox.SelectionChanged += (_, _) =>
        {
            if (modeBox.SelectedItem is ComboBoxItem { Tag: string mode })
            {
                Online.SetPlaybackMode(mode);
            }
        };

        var clearButton = new Button { Content = "清空历史" };
        clearButton.Click += async (_, _) => await Online.ClearHistoryAsync();

        var content = new StackPanel
        {
            MinWidth = 480,
            Spacing = 10,
        };
        content.Children.Add(modeBox);
        content.Children.Add(historyList);
        content.Children.Add(clearButton);

        var dialog = new ContentDialog
        {
            Title = "历史播放",
            Content = content,
            CloseButtonText = "关闭",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowLocalizedAsync();
    }

    private async void DeleteHistoryItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: OnlineHistoryItem item })
        {
            await Online.RemoveHistoryAsync(item);
        }
    }
}
