using LiteMCLauncher.Models;
using LiteMCLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LiteMCLauncher.Pages;

public sealed partial class MusicPage : Page
{
    private bool _loadingRepeatMode;

    public MusicService Music => AppServices.Music;

    public MusicPage()
    {
        InitializeComponent();
        Loaded += MusicPage_Loaded;
    }

    private async void MusicPage_Loaded(object sender, RoutedEventArgs e)
    {
        await Music.InitializeAsync();
        UpdateRepeatModeBox();
    }

    private async void ImportMusic_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".flac");
        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".m4a");
        picker.FileTypeFilter.Add(".aac");
        picker.FileTypeFilter.Add(".ogg");
        picker.FileTypeFilter.Add(".opus");
        picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));

        var files = await picker.PickMultipleFilesAsync();
        if (files is not null && files.Count > 0)
        {
            await Music.ImportFilesAsync(files);
        }
    }

    private async void MusicList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is MusicTrack track)
        {
            await Music.PlayTrackAsync(track);
        }
    }

    private async void RemoveTrack_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MusicTrack track })
        {
            await Music.RemoveTrackAsync(track);
        }
    }

    private async void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        await Music.TogglePlayPauseAsync();
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        await Music.NextAsync();
    }

    private async void Previous_Click(object sender, RoutedEventArgs e)
    {
        await Music.PreviousAsync();
    }

    private void RepeatModeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingRepeatMode)
        {
            return;
        }

        Music.RepeatMode = RepeatModeBox.SelectedIndex switch
        {
            1 => MusicRepeatMode.Single,
            2 => MusicRepeatMode.Shuffle,
            _ => MusicRepeatMode.List,
        };
    }

    private void UpdateRepeatModeBox()
    {
        _loadingRepeatMode = true;
        try
        {
            RepeatModeBox.SelectedIndex = Music.RepeatMode switch
            {
                MusicRepeatMode.Single => 1,
                MusicRepeatMode.Shuffle => 2,
                _ => 0,
            };
        }
        finally
        {
            _loadingRepeatMode = false;
        }
    }
}
