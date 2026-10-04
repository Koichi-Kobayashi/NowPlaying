using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using NowPlaying.Models;
using Windows.Media.Control;

namespace NowPlaying.Services;

/// <summary>
/// 再生中のメディア情報を取得するサービス
/// </summary>
public partial class NowPlayingService : ObservableObject
{
    [ObservableProperty]
    private NowPlayingTrack _currentTrack = new();

    private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
    private GlobalSystemMediaTransportControlsSession? _currentSession;
    private DispatcherTimer? _pollTimer;
    private readonly YouTubeArtworkService _youTubeArtworkService = new();
    private long _updateVersion;
    private string? _enhancedArtworkKey;
    private BitmapSource? _enhancedArtwork;

    public event EventHandler? CurrentTrackChanged;

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;

            await UpdateFromCurrentSessionAsync();

            _pollTimer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher)
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _pollTimer.Tick += (_, _) => _ = UpdateFromCurrentSessionAsync();
            _pollTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NowPlayingService init error: {ex.Message}");
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            _currentSession = sender.GetCurrentSession();
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
            }
            await UpdateFromCurrentSessionAsync();
        });
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(UpdateFromCurrentSessionAsync);
    }

    private async Task UpdateFromCurrentSessionAsync()
    {
        var version = ++_updateVersion;
        try
        {
            if (_currentSession != null)
            {
                _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            }

            _currentSession = _sessionManager?.GetCurrentSession();
            if (_currentSession == null)
            {
                CurrentTrack = new NowPlayingTrack();
                CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;

            var session = _currentSession;
            var props = await session.TryGetMediaPropertiesAsync();
            if (version != _updateVersion)
                return;
            var playbackInfo = session.GetPlaybackInfo();

            if (props == null)
            {
                CurrentTrack = new NowPlayingTrack();
                CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            ImageSource? artwork = null;
            if (props.Thumbnail != null)
            {
                try
                {
                    artwork = await GetBitmapFromStreamAsync(props.Thumbnail);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Thumbnail error: {ex.Message}");
                }
            }

            var title = props.Title ?? string.Empty;
            var artist = props.Artist ?? string.Empty;
            var albumTitle = props.AlbumTitle ?? string.Empty;
            if (version != _updateVersion)
                return;
            var artworkKey = $"{session.SourceAppUserModelId}\n{title}\n{artist}";
            if (artworkKey == _enhancedArtworkKey)
                artwork = _enhancedArtwork ?? artwork;
            else
            {
                _enhancedArtworkKey = artworkKey;
                _enhancedArtwork = null;
            }

            // 一部のプレイヤーが Artist に「アーティスト — アルバム」形式で渡す場合をパース
            if (string.IsNullOrWhiteSpace(albumTitle) && !string.IsNullOrWhiteSpace(artist))
            {
                var split = SplitArtistAndAlbum(artist);
                if (split.HasValue)
                {
                    artist = split.Value.Artist;
                    albumTitle = split.Value.Album;
                }
            }

            if (version != _updateVersion)
                return;
            var track = new NowPlayingTrack
            {
                Title = title,
                Artist = artist,
                AlbumTitle = albumTitle,
                AlbumArtwork = artwork,
                IsPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            };

            CurrentTrack = track;
            CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
            _ = UpgradeYouTubeArtworkAsync(session.SourceAppUserModelId, props.Title ?? string.Empty, props.Artist ?? string.Empty,
                artworkKey, track, version);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update track error: {ex.Message}");
            if (version == _updateVersion)
                CurrentTrack = new NowPlayingTrack();
        }
    }

    private async Task UpgradeYouTubeArtworkAsync(string sourceAppId, string title, string artist,
        string artworkKey, NowPlayingTrack track, long version)
    {
        var artwork = await _youTubeArtworkService.GetArtworkAsync(sourceAppId, title, artist);
        if (artwork == null || version != _updateVersion || !ReferenceEquals(CurrentTrack, track))
            return;
        if (track.AlbumArtwork is BitmapSource original && original.PixelWidth >= artwork.PixelWidth)
            return;

        _enhancedArtworkKey = artworkKey;
        _enhancedArtwork = artwork;
        CurrentTrack = new NowPlayingTrack
        {
            Title = track.Title,
            Artist = track.Artist,
            AlbumTitle = track.AlbumTitle,
            AlbumArtwork = artwork,
            IsPlaying = track.IsPlaying
        };
    }

    /// <summary>
    /// 「アーティスト — アルバム」形式の文字列を分割する。
    /// 一部のメディアプレイヤーが Artist フィールドにアーティストとアルバムを結合して渡す場合に対応。
    /// </summary>
    private static (string Artist, string Album)? SplitArtistAndAlbum(string combined)
    {
        var separators = new[] { " — ", " – ", " - " };
        foreach (var sep in separators)
        {
            var idx = combined.IndexOf(sep, StringComparison.Ordinal);
            if (idx <= 0) continue;

            var artist = combined[..idx].Trim();
            var album = combined[(idx + sep.Length)..].Trim();
            if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(album))
                return (artist, album);
        }
        return null;
    }

    private static async Task<ImageSource?> GetBitmapFromStreamAsync(Windows.Storage.Streams.IRandomAccessStreamReference streamRef)
    {
        using var stream = await streamRef.OpenReadAsync();
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream.AsStream();
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public void Refresh()
    {
        _ = UpdateFromCurrentSessionAsync();
    }
}
