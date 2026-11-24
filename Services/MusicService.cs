using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using LiteMCLauncher.Models;
using LiteMCLauncher.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace LiteMCLauncher.Services;

/// <summary>
/// Local music library and playback. Uses Windows MediaPlayer so the launcher
/// can play MP3/FLAC/M4A/WAV/OGG files without extra codecs.
/// </summary>
public sealed class MusicService : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly DispatcherQueue? _dispatcherQueue;
    private Task? _initializeTask;
    private MediaPlayer? _player;
    private MediaSource? _mediaSource;
    private MusicTrack? _currentTrack;
    private bool _isPlaying;
    private bool _playOnLauncherStart;
    private bool _playOnGameStart;
    private MusicRepeatMode _repeatMode = MusicRepeatMode.List;
    private double _volume = 0.8;
    private string? _statusText;
    private int _currentIndex = -1;
    private bool _handlingEnd;
    private bool _suppressSave;
    private bool _isOverlayVisible;

    public MusicService()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        Tracks.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTracks));
            OnPropertyChanged(nameof(TrackCountText));
        };
    }

    public ObservableCollection<MusicTrack> Tracks { get; } = [];

    public bool HasTracks => Tracks.Count > 0;

    public string TrackCountText => $"{Tracks.Count} 首";

    public MusicTrack? CurrentTrack
    {
        get => _currentTrack;
        private set
        {
            if (SetProperty(ref _currentTrack, value))
            {
                OnPropertyChanged(nameof(HasCurrentTrack));
                OnPropertyChanged(nameof(CurrentTitle));
                OnPropertyChanged(nameof(CurrentArtist));
                OnPropertyChanged(nameof(CurrentAlbum));
                OnPropertyChanged(nameof(CurrentAlbumArt));
            }
        }
    }

    public bool HasCurrentTrack => CurrentTrack is not null;

    public bool IsOverlayVisible => _isOverlayVisible;

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseGlyph));
            }
        }
    }

    public string CurrentTitle => CurrentTrack?.Title ?? "未在播放";

    public string CurrentArtist => CurrentTrack?.Artist ?? string.Empty;

    public string CurrentAlbum => CurrentTrack?.Album ?? string.Empty;

    public ImageSource? CurrentAlbumArt => CurrentTrack?.AlbumArt;

    public string PlayPauseGlyph => IsPlaying ? "\uE769" : "\uE768";

    public MusicRepeatMode RepeatMode
    {
        get => _repeatMode;
        set
        {
            if (SetProperty(ref _repeatMode, value))
            {
                OnPropertyChanged(nameof(RepeatModeDisplay));
                _ = SaveAsync();
            }
        }
    }

    public string RepeatModeDisplay => RepeatMode switch
    {
        MusicRepeatMode.Single => "单曲循环",
        MusicRepeatMode.Shuffle => "随机播放",
        _ => "列表循环",
    };

    public bool PlayOnLauncherStart
    {
        get => _playOnLauncherStart;
        set
        {
            if (SetProperty(ref _playOnLauncherStart, value))
            {
                _ = SaveAsync();
            }
        }
    }

    public bool PlayOnGameStart
    {
        get => _playOnGameStart;
        set
        {
            if (SetProperty(ref _playOnGameStart, value))
            {
                _ = SaveAsync();
            }
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            var normalized = Math.Clamp(value, 0, 1);
            if (SetProperty(ref _volume, normalized))
            {
                if (_player is not null)
                {
                    _player.Volume = normalized;
                }

                _ = SaveAsync();
            }
        }
    }

    public string? StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public Task InitializeAsync()
    {
        if (_initializeTask is null)
        {
            _initializeTask = InitializeCoreAsync();
        }

        return _initializeTask;
    }

    private async Task InitializeCoreAsync()
    {
        EnsurePlayer();
        var settings = await LoadSettingsAsync();

        _suppressSave = true;
        try
        {
            RepeatMode = settings.RepeatMode;
            PlayOnLauncherStart = settings.PlayOnLauncherStart;
            PlayOnGameStart = settings.PlayOnGameStart;
            Volume = settings.Volume;
        }
        finally
        {
            _suppressSave = false;
        }

        foreach (var path in settings.Tracks)
        {
            if (!File.Exists(path) ||
                Tracks.Any(track => string.Equals(track.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var track = await LoadTrackAsync(path);
            if (track is not null)
            {
                Tracks.Add(track);
            }
        }

        _currentIndex = settings.CurrentTrackPath is null
            ? -1
            : Tracks.ToList().FindIndex(track =>
                string.Equals(track.FilePath, settings.CurrentTrackPath, StringComparison.OrdinalIgnoreCase));

        if (_currentIndex >= 0)
        {
            CurrentTrack = Tracks[_currentIndex];
        }

        if (PlayOnLauncherStart && Tracks.Count > 0)
        {
            await PlayAsync(_currentIndex >= 0 ? _currentIndex : 0);
        }
    }

    public async Task<int> ImportFilesAsync(
        IReadOnlyList<StorageFile> files,
        CancellationToken cancellationToken = default)
    {
        var added = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (Tracks.Any(track =>
                    string.Equals(track.FilePath, file.Path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var track = await LoadTrackAsync(file.Path);
            if (track is null)
            {
                continue;
            }

            Tracks.Add(track);
            added++;
        }

        if (added > 0)
        {
            if (CurrentTrack is null)
            {
                _currentIndex = 0;
                CurrentTrack = Tracks[0];
            }

            await SaveAsync();
        }

        return added;
    }

    public async Task PlayAsync(int index)
    {
        if (index < 0 || index >= Tracks.Count)
        {
            return;
        }

        EnsurePlayer();
        AppServices.OnlineMusic.Pause();

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(Tracks[index].FilePath);
            _mediaSource?.Dispose();
            _mediaSource = MediaSource.CreateFromStorageFile(file);
            _player!.Source = _mediaSource;
            _currentIndex = index;
            CurrentTrack = Tracks[index];
            StatusText = null;
            _player.Play();
            IsPlaying = true;
            SetOverlayVisible(true);
            await SaveAsync();
        }
        catch (Exception exception)
        {
            StatusText = $"无法播放：{exception.Message}";
            IsPlaying = false;
        }
    }

    public async Task TogglePlayPauseAsync()
    {
        EnsurePlayer();

        if (CurrentTrack is null)
        {
            if (Tracks.Count > 0)
            {
                await PlayAsync(0);
            }

            return;
        }

        if (_player!.Source is null)
        {
            await PlayAsync(_currentIndex >= 0 ? _currentIndex : 0);
            return;
        }

        if (_player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
        {
            _player.Pause();
            IsPlaying = false;
        }
        else
        {
            AppServices.OnlineMusic.Pause();
            _player.Play();
            IsPlaying = true;
        }

        await Task.CompletedTask;
    }

    public void Pause()
    {
        if (!IsPlaying)
        {
            return;
        }

        _player?.Pause();
        IsPlaying = false;
    }

    public async Task NextAsync()
    {
        if (Tracks.Count == 0)
        {
            return;
        }

        var next = GetNextIndex(manual: true);
        await PlayAsync(next);
    }

    public async Task PreviousAsync()
    {
        if (Tracks.Count == 0)
        {
            return;
        }

        var next = RepeatMode == MusicRepeatMode.Shuffle
            ? GetRandomIndex()
            : (_currentIndex - 1 + Tracks.Count) % Tracks.Count;
        await PlayAsync(next);
    }

    public async Task PlayTrackAsync(MusicTrack track)
    {
        var index = Tracks.IndexOf(track);
        if (index >= 0)
        {
            await PlayAsync(index);
        }
    }

    public async Task RemoveTrackAsync(MusicTrack track)
    {
        var index = Tracks.IndexOf(track);
        if (index < 0)
        {
            return;
        }

        var wasCurrent = index == _currentIndex;
        Tracks.RemoveAt(index);

        if (wasCurrent)
        {
            _player?.Pause();
            _player?.Source = null;
            _mediaSource?.Dispose();
            _mediaSource = null;
            IsPlaying = false;
            CurrentTrack = Tracks.Count == 0 ? null : Tracks[Math.Min(index, Tracks.Count - 1)];
            _currentIndex = CurrentTrack is null ? -1 : Tracks.IndexOf(CurrentTrack);
            if (CurrentTrack is null)
            {
                SetOverlayVisible(false);
            }
        }
        else if (index < _currentIndex)
        {
            _currentIndex--;
        }

        await SaveAsync();
    }

    public void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            MusicRepeatMode.List => MusicRepeatMode.Single,
            MusicRepeatMode.Single => MusicRepeatMode.Shuffle,
            _ => MusicRepeatMode.List,
        };
    }

    public async Task HandleGameStartedAsync()
    {
        if (!PlayOnGameStart || Tracks.Count == 0)
        {
            return;
        }

        if (CurrentTrack is null)
        {
            await PlayAsync(0);
        }
        else if (_player?.Source is null)
        {
            await PlayAsync(_currentIndex >= 0 ? _currentIndex : 0);
        }
        else if (_player.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
        {
            _player.Play();
            IsPlaying = true;
        }
    }

    private int GetNextIndex(bool manual)
    {
        if (Tracks.Count == 0)
        {
            return -1;
        }

        if (RepeatMode == MusicRepeatMode.Shuffle)
        {
            return GetRandomIndex();
        }

        return (_currentIndex + 1) % Tracks.Count;
    }

    private int GetRandomIndex()
    {
        if (Tracks.Count <= 1)
        {
            return 0;
        }

        var index = Random.Shared.Next(Tracks.Count - 1);
        return index >= _currentIndex ? index + 1 : index;
    }

    private void EnsurePlayer()
    {
        if (_player is not null)
        {
            return;
        }

        _player = new MediaPlayer
        {
            AutoPlay = false,
            Volume = _volume,
        };

        _player.MediaEnded += (_, _) =>
        {
            _dispatcherQueue?.TryEnqueue(async () => await HandleMediaEndedAsync());
        };

        _player.MediaFailed += (_, args) =>
        {
            _dispatcherQueue?.TryEnqueue(() =>
            {
                StatusText = $"播放失败：{args.ErrorMessage}";
                IsPlaying = false;
            });
        };

        _player.PlaybackSession.PlaybackStateChanged += (_, _) =>
        {
            _dispatcherQueue?.TryEnqueue(() =>
            {
                IsPlaying = _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
            });
        };
    }

    private async Task HandleMediaEndedAsync()
    {
        if (_handlingEnd)
        {
            return;
        }

        _handlingEnd = true;
        try
        {
            if (RepeatMode == MusicRepeatMode.Single && _currentIndex >= 0)
            {
                _player!.PlaybackSession.Position = TimeSpan.Zero;
                _player.Play();
                IsPlaying = true;
                return;
            }

            var next = GetNextIndex(manual: false);
            if (next >= 0)
            {
                await PlayAsync(next);
            }
        }
        finally
        {
            _handlingEnd = false;
        }
    }

    private async Task<MusicTrack?> LoadTrackAsync(string path)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            var properties = await file.Properties.GetMusicPropertiesAsync();

            var title = string.IsNullOrWhiteSpace(properties.Title)
                ? Path.GetFileNameWithoutExtension(path)
                : properties.Title;
            var track = new MusicTrack
            {
                FilePath = path,
                Title = title,
                Artist = string.IsNullOrWhiteSpace(properties.Artist)
                    ? "未知艺术家"
                    : properties.Artist,
                Album = string.IsNullOrWhiteSpace(properties.Album)
                    ? "未知专辑"
                    : properties.Album,
                Duration = properties.Duration,
            };

            try
            {
                using var thumbnail = await file.GetThumbnailAsync(
                    ThumbnailMode.MusicView,
                    256,
                    ThumbnailOptions.UseCurrentScale);

                if (thumbnail is not null)
                {
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(thumbnail);
                    track.AlbumArt = bitmap;
                }
            }
            catch
            {
                // Some files have no embedded album art.
            }

            return track;
        }
        catch
        {
            return null;
        }
    }

    private async Task<MusicSettings> LoadSettingsAsync()
    {
        try
        {
            if (File.Exists(MusicSettingsPath))
            {
                var json = await File.ReadAllTextAsync(MusicSettingsPath);
                var settings = JsonSerializer.Deserialize<MusicSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch
        {
        }

        return new MusicSettings();
    }

    private async Task SaveAsync()
    {
        if (_suppressSave)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var settings = new MusicSettings
            {
                Tracks = Tracks.Select(track => track.FilePath).ToList(),
                CurrentTrackPath = CurrentTrack?.FilePath,
                RepeatMode = RepeatMode,
                PlayOnLauncherStart = PlayOnLauncherStart,
                PlayOnGameStart = PlayOnGameStart,
                Volume = Volume,
            };

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await File.WriteAllTextAsync(MusicSettingsPath, json);
        }
        catch
        {
            // Music persistence must not break playback.
        }
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_isOverlayVisible == visible)
        {
            return;
        }

        _isOverlayVisible = visible;
        OnPropertyChanged(nameof(IsOverlayVisible));
    }

    private static string MusicSettingsPath =>
        Path.Combine(AppPaths.DataDirectory, "music.json");

    private sealed class MusicSettings
    {
        public List<string> Tracks { get; set; } = [];

        public string? CurrentTrackPath { get; set; }

        public MusicRepeatMode RepeatMode { get; set; } = MusicRepeatMode.List;

        public bool PlayOnLauncherStart { get; set; }

        public bool PlayOnGameStart { get; set; }

        public double Volume { get; set; } = 0.8;
    }
}
