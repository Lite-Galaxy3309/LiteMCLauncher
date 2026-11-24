using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LiteMCLauncher.Models;
using LiteMCLauncher.ViewModels;
using Microsoft.UI.Dispatching;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace LiteMCLauncher.Services;

/// <summary>
/// NetEase Cloud Music search and playback. The user's MUSIC_U cookie can be
/// supplied through the online music settings; it is stored only in local app
/// data and is sent as the Cookie request header.
/// </summary>
public sealed class OnlineMusicService : ObservableObject
{
    private const string SearchEndpoint = "https://music.163.com/api/cloudsearch/pc";
    private const string SongUrlEndpoint = "https://interface3.music.163.com/eapi/song/enhance/player/url/v1";
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Safari/537.36 Chrome/91.0.4472.164 NeteaseMusicDesktop/2.10.2.200154";
    private const string Referer = "https://music.163.com/";
    private const string AesKey = "e82ckenh8dichen8";

    private static readonly HttpClient HttpClient = CreateHttpClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly MediaPlayer _player;
    private MediaSource? _mediaSource;
    private readonly DispatcherQueue? _dispatcherQueue;
    private Task? _initializeTask;
    private OnlineSong? _currentSong;
    private string _cookie = string.Empty;
    private string _statusText = "输入关键词搜索网易云音乐";
    private bool _isSearching;
    private bool _isPlaying;
    private string _playbackMode = "RepeatAll";
    private readonly List<OnlineSong> _queue = [];
    private int _queueIndex = -1;

    public OnlineMusicService()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _player = new MediaPlayer
        {
            AudioCategory = MediaPlayerAudioCategory.Media,
        };
        _player.MediaEnded += (_, _) =>
        {
            SetPlaying(false);
            _ = AdvanceAfterEndAsync();
        };
        _player.MediaFailed += (_, args) =>
        {
            SetStatus($"播放失败：{args.ErrorMessage}");
            SetPlaying(false);
        };
    }

    public ObservableCollection<OnlineSong> Results { get; } = [];

    public ObservableCollection<OnlineHistoryItem> History { get; } = [];

    public ObservableCollection<string> SearchHistory { get; } = [];

    public OnlineSong? CurrentSong
    {
        get => _currentSong;
        private set
        {
            if (SetProperty(ref _currentSong, value))
            {
                OnPropertyChanged(nameof(CurrentTitle));
                OnPropertyChanged(nameof(CurrentArtist));
                OnPropertyChanged(nameof(CurrentCoverUrl));
                OnPropertyChanged(nameof(CurrentAlbum));
                OnPropertyChanged(nameof(HasCurrentSong));
            }
        }
    }

    public string CurrentTitle => CurrentSong?.Name ?? "未在播放";

    public string CurrentArtist => CurrentSong?.Artists ?? "搜索并播放网易云音乐";

    public string CurrentCoverUrl => CurrentSong?.CoverUrl ?? string.Empty;

    public string CurrentAlbum => CurrentSong?.Album ?? string.Empty;

    public bool HasCurrentSong => CurrentSong is not null;

    public string PlaybackMode
    {
        get => _playbackMode;
        private set
        {
            if (SetProperty(ref _playbackMode, value))
            {
                OnPropertyChanged(nameof(PlaybackModeDisplay));
            }
        }
    }

    public string PlaybackModeDisplay => PlaybackMode switch
    {
        "RepeatOne" => "单曲循环",
        "Shuffle" => "随机播放",
        _ => "列表循环",
    };

    public bool HasQueue => _queue.Count > 1;

    public bool IsSearching
    {
        get => _isSearching;
        private set => SetProperty(ref _isSearching, value);
    }

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

    public string PlayPauseGlyph => IsPlaying ? "\uE769" : "\uE768";

    public string Cookie
    {
        get => _cookie;
        private set => SetProperty(ref _cookie, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public Task InitializeAsync()
    {
        return _initializeTask ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            var settings = OnlineMusicSettings.Load();
            Cookie = settings.Cookie ?? string.Empty;
            if (settings.PlaybackMode is "RepeatAll" or "RepeatOne" or "Shuffle")
            {
                PlaybackMode = settings.PlaybackMode;
            }

            foreach (var item in settings.History ?? [])
            {
                History.Add(item);
            }

            foreach (var keyword in (settings.SearchHistory ?? []).Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                SearchHistory.Add(keyword);
            }
        }
        catch
        {
            StatusText = "音乐历史读取失败，可继续搜索。";
        }

        await Task.CompletedTask;
    }

    public async Task<IReadOnlyList<OnlineSong>> SearchAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        keyword = keyword?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        IsSearching = true;
        StatusText = $"正在搜索“{keyword}”…";
        try
        {
            using var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("s", keyword),
                new KeyValuePair<string, string>("type", "1"),
                new KeyValuePair<string, string>("limit", "30"),
            ]);
            using var request = new HttpRequestMessage(HttpMethod.Post, SearchEndpoint)
            {
                Content = content,
            };
            AddHeaders(request, includeReferer: true);

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var root = document.RootElement;
            if (root.TryGetProperty("code", out var code) && code.GetInt32() != 200)
            {
                StatusText = $"搜索失败：{GetString(root, "message") ?? "网易云接口返回异常"}";
                return [];
            }

            var songs = new List<OnlineSong>();
            if (root.TryGetProperty("result", out var result) &&
                result.TryGetProperty("songs", out var songArray))
            {
                foreach (var item in songArray.EnumerateArray())
                {
                    var song = ParseSong(item);
                    if (song is not null)
                    {
                        songs.Add(song);
                    }
                }
            }

            Results.Clear();
            foreach (var song in songs)
            {
                Results.Add(song);
            }

            AddSearchKeyword(keyword);
            StatusText = songs.Count == 0
                ? "没有找到匹配的歌曲。"
                : $"找到 {songs.Count} 首歌曲。";
            return songs;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            StatusText = $"搜索失败：{exception.Message}";
            return [];
        }
        finally
        {
            IsSearching = false;
        }
    }

    public async Task<OnlineSong?> GetSongUrlAsync(
        OnlineSong song,
        CancellationToken cancellationToken = default)
    {
        if (song is null)
        {
            return null;
        }

        try
        {
            var payload = BuildSongPayload(song.Id, "standard");
            var url = await RequestSongUrlAsync(payload, cancellationToken);
            if (string.IsNullOrWhiteSpace(url))
            {
                StatusText = "该歌曲没有可用的播放地址，可能需要登录或没有版权。";
                return null;
            }

            song.PlayUrl = url;
            return song;
        }
        catch (Exception exception)
        {
            StatusText = $"获取播放地址失败：{exception.Message}";
            return null;
        }
    }

    public Task PlayAsync(OnlineSong song, CancellationToken cancellationToken = default)
    {
        return PlayAsync(song, queue: null, cancellationToken);
    }

    public async Task PlayAsync(
        OnlineSong song,
        IEnumerable<OnlineSong>? queue,
        CancellationToken cancellationToken = default)
    {
        if (song is null)
        {
            return;
        }

        SetQueue(queue, song);
        await PlayQueueIndexAsync(_queueIndex, cancellationToken);
    }

    private void SetQueue(IEnumerable<OnlineSong>? queue, OnlineSong current)
    {
        _queue.Clear();
        if (queue is not null)
        {
            foreach (var item in queue)
            {
                _queue.Add(item);
            }
        }

        if (_queue.Count == 0)
        {
            _queue.Add(current);
        }

        _queueIndex = _queue.FindIndex(item => item.Id == current.Id);
        if (_queueIndex < 0)
        {
            _queue.Insert(0, current);
            _queueIndex = 0;
        }

        OnPropertyChanged(nameof(HasQueue));
    }

    private async Task PlayQueueIndexAsync(int index, CancellationToken cancellationToken = default)
    {
        if (index < 0 || index >= _queue.Count)
        {
            return;
        }

        var song = _queue[index];
        var playable = await GetSongUrlAsync(song, cancellationToken);
        if (playable?.PlayUrl is null)
        {
            return;
        }

        _queueIndex = index;
        AppServices.Music.Pause();
        CurrentSong = playable;
        _player.Pause();
        _mediaSource?.Dispose();
        _mediaSource = MediaSource.CreateFromUri(new Uri(playable.PlayUrl));
        _player.Source = _mediaSource;
        _player.Play();
        SetPlaying(true);
        StatusText = $"正在播放：{playable.Name}";
        AddHistory(playable);
        await SaveAsync();
    }

    public async Task NextAsync(bool manual = true)
    {
        if (_queue.Count == 0)
        {
            return;
        }

        if (!manual && PlaybackMode == "RepeatOne")
        {
            await ReplayCurrentAsync();
            return;
        }

        var nextIndex = PlaybackMode == "Shuffle"
            ? GetRandomQueueIndex()
            : _queueIndex + 1;
        if (nextIndex >= _queue.Count)
        {
            nextIndex = 0;
        }

        await PlayQueueIndexAsync(nextIndex);
    }

    public async Task PreviousAsync()
    {
        if (_queue.Count == 0)
        {
            return;
        }

        var previousIndex = PlaybackMode == "Shuffle"
            ? GetRandomQueueIndex()
            : _queueIndex - 1;
        if (previousIndex < 0)
        {
            previousIndex = _queue.Count - 1;
        }

        await PlayQueueIndexAsync(previousIndex);
    }

    private int GetRandomQueueIndex()
    {
        if (_queue.Count <= 1)
        {
            return 0;
        }

        var index = _queueIndex;
        while (index == _queueIndex)
        {
            index = Random.Shared.Next(_queue.Count);
        }

        return index;
    }

    private async Task ReplayCurrentAsync()
    {
        if (CurrentSong is null)
        {
            return;
        }

        if (_player.Source is null && !string.IsNullOrWhiteSpace(CurrentSong.PlayUrl))
        {
            _mediaSource?.Dispose();
            _mediaSource = MediaSource.CreateFromUri(new Uri(CurrentSong.PlayUrl));
            _player.Source = _mediaSource;
        }

        if (_player.Source is null)
        {
            await PlayQueueIndexAsync(_queueIndex);
            return;
        }

        _player.Position = TimeSpan.Zero;
        _player.Play();
        SetPlaying(true);
        StatusText = $"正在播放：{CurrentSong.Name}";
    }

    private async Task AdvanceAfterEndAsync()
    {
        if (_queue.Count == 0)
        {
            return;
        }

        void Advance()
        {
            _ = NextAsync(manual: false);
        }

        if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(Advance);
        }
        else
        {
            Advance();
        }

        await Task.CompletedTask;
    }

    public void CyclePlaybackMode()
    {
        PlaybackMode = PlaybackMode switch
        {
            "RepeatAll" => "RepeatOne",
        "RepeatOne" => "单曲循环",
            _ => "RepeatAll",
        };
        _ = SaveAsync();
    }

    public void SetPlaybackMode(string mode)
    {
        if (mode is "RepeatAll" or "RepeatOne" or "Shuffle")
        {
            PlaybackMode = mode;
        }
    }

    public Task PlayHistoryAsync(OnlineHistoryItem item, CancellationToken cancellationToken = default)
    {
        var queue = History
            .Select(historyItem => new OnlineSong
            {
                Id = historyItem.Id,
                Name = historyItem.Name,
                Artists = historyItem.Artists,
                CoverUrl = historyItem.CoverUrl,
            })
            .ToList();
        var song = queue.FirstOrDefault(candidate => candidate.Id == item.Id) ?? new OnlineSong
        {
            Id = item.Id,
            Name = item.Name,
            Artists = item.Artists,
            CoverUrl = item.CoverUrl,
        };
        return PlayAsync(song, queue, cancellationToken);
    }

    public async Task RemoveHistoryAsync(OnlineHistoryItem item)
    {
        if (item is null)
        {
            return;
        }

        History.Remove(item);
        await SaveAsync();
    }

    public async Task ClearHistoryAsync()
    {
        History.Clear();
        await SaveAsync();
    }

    public void TogglePlayPause()
    {
        if (CurrentSong is null)
        {
            return;
        }

        if (IsPlaying)
        {
            _player.Pause();
            SetPlaying(false);
            StatusText = "已暂停。";
        }
        else
        {
            AppServices.Music.Pause();
            _player.Play();
            SetPlaying(true);
            StatusText = $"正在播放：{CurrentSong.Name}";
        }
    }

    public void Pause()
    {
        if (!IsPlaying)
        {
            return;
        }

        _player.Pause();
        SetPlaying(false);
    }

    public async Task SaveCookieAsync(string? cookie)
    {
        Cookie = string.IsNullOrWhiteSpace(cookie) ? string.Empty : cookie.Trim();
        await SaveAsync();
        StatusText = string.IsNullOrWhiteSpace(Cookie)
            ? "已清除网易云 Cookie，将以游客身份搜索。"
            : "网易云 Cookie 已保存。搜索和播放将使用你的账号信息。";
    }

    private static OnlineSong? ParseSong(JsonElement item)
    {
        try
        {
            var id = item.TryGetProperty("id", out var idElement) ? idElement.GetInt64() : 0;
            var name = GetString(item, "name") ?? string.Empty;
            if (id == 0 || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            var artists = new List<string>();
            if (item.TryGetProperty("ar", out var artistsElement))
            {
                foreach (var artist in artistsElement.EnumerateArray())
                {
                    var artistName = GetString(artist, "name");
                    if (!string.IsNullOrWhiteSpace(artistName))
                    {
                        artists.Add(artistName);
                    }
                }
            }

            var album = string.Empty;
            var cover = string.Empty;
            if (item.TryGetProperty("al", out var albumElement))
            {
                album = GetString(albumElement, "name") ?? string.Empty;
                cover = GetString(albumElement, "picUrl") ?? string.Empty;
            }

            var duration = item.TryGetProperty("dt", out var durationElement) &&
                           durationElement.TryGetInt32(out var durationValue)
                ? durationValue
                : 0;

            return new OnlineSong
            {
                Id = id,
                Name = name,
                Artists = artists.Count == 0 ? "未知歌手" : string.Join(" / ", artists),
                Album = album,
                CoverUrl = cover,
                DurationMs = duration,
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> RequestSongUrlAsync(string payload, CancellationToken cancellationToken)
    {
        var encrypted = EncryptParams(payload);
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("params", encrypted),
        ]);
        using var request = new HttpRequestMessage(HttpMethod.Post, SongUrlEndpoint)
        {
            Content = content,
        };
        AddHeaders(request, includeReferer: false);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var url = GetString(item, "url");
                if (!string.IsNullOrWhiteSpace(url))
                {
                    return url;
                }
            }
        }

        return null;
    }

    private static string BuildSongPayload(long songId, string level)
    {
        var requestId = Random.Shared.Next(20_000_000, 30_000_000);
        var header = $"{{\"os\": \"pc\", \"appver\": \"8.9.75\", \"osver\": \"\", \"deviceId\": \"pyncm!\", \"requestId\": \"{requestId}\"}}";
        var headerJson = JsonSerializer.Serialize(header);
        return $"{{\"ids\": [{songId}], \"level\": \"{level}\", \"encodeType\": \"flac\", \"header\": {headerJson}}}";
    }

    private static string EncryptParams(string payload)
    {
        const string urlPath = "/api/song/enhance/player/url/v1";
        var digest = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(
            $"nobody{urlPath}use{payload}md5forencrypt"))).ToLowerInvariant();
        var text = $"{urlPath}-36cd479b6b5-{payload}-36cd479b6b5-{digest}";
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(AesKey);
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(text);
        return Convert.ToHexString(encryptor.TransformFinalBlock(bytes, 0, bytes.Length)).ToLowerInvariant();
    }

    private void AddHeaders(HttpRequestMessage request, bool includeReferer = true)
    {
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        if (includeReferer)
        {
            request.Headers.TryAddWithoutValidation("Referer", Referer);
        }
        if (!string.IsNullOrWhiteSpace(Cookie))
        {
            var value = Cookie.Trim();
            if (value.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
            {
                value = value[7..].Trim();
            }

            if (!value.Contains('='))
            {
                value = $"MUSIC_U={value}";
            }

            request.Headers.TryAddWithoutValidation("Cookie", value);
        }
        else
        {
            request.Headers.TryAddWithoutValidation("Cookie", "os=pc; appver=8.9.75; osver=; deviceId=pyncm!");
        }
    }

    private void AddSearchKeyword(string keyword)
    {
        var existing = SearchHistory.FirstOrDefault(value =>
            string.Equals(value, keyword, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SearchHistory.Remove(existing);
        }

        SearchHistory.Insert(0, keyword);
        while (SearchHistory.Count > 20)
        {
            SearchHistory.RemoveAt(SearchHistory.Count - 1);
        }
    }

    private void AddHistory(OnlineSong song)
    {
        var existing = History.FirstOrDefault(item => item.Id == song.Id);
        if (existing is not null)
        {
            History.Remove(existing);
        }

        History.Insert(0, new OnlineHistoryItem
        {
            Id = song.Id,
            Name = song.Name,
            Artists = song.Artists,
            CoverUrl = song.CoverUrl,
            PlayedAt = DateTimeOffset.UtcNow,
        });

        while (History.Count > 30)
        {
            History.RemoveAt(History.Count - 1);
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await OnlineMusicSettings.SaveAsync(new OnlineMusicSettings
            {
                Cookie = Cookie,
                History = History.ToList(),
                SearchHistory = SearchHistory.ToList(),
                PlaybackMode = PlaybackMode,
            });
        }
        catch
        {
            // Online music data must never prevent playback.
        }
    }

    private void SetPlaying(bool value)
    {
        if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => SetPlaying(value));
            return;
        }

        IsPlaying = value;
    }

    private void SetStatus(string value)
    {
        if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => StatusText = value);
            return;
        }

        StatusText = value;
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }
}

public sealed class OnlineMusicSettings
{
    public string? Cookie { get; set; }

    public List<OnlineHistoryItem>? History { get; set; }

    public List<string>? SearchHistory { get; set; }

    public string? PlaybackMode { get; set; }

    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "online-music.json");

    public static OnlineMusicSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<OnlineMusicSettings>(File.ReadAllText(FilePath)) ??
                       new OnlineMusicSettings();
            }
        }
        catch
        {
        }

        return new OnlineMusicSettings();
    }

    public static async Task SaveAsync(OnlineMusicSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDirectory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        var temp = FilePath + ".tmp";
        await File.WriteAllTextAsync(temp, json);
        File.Move(temp, FilePath, overwrite: true);
    }
}
