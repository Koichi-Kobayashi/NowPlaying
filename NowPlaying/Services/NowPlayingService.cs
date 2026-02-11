using System.IO;
using System.Windows;
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

    public event EventHandler? CurrentTrackChanged;

    public async Task InitializeAsync()
    {
        try
        {
            _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            _sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;

            await UpdateFromCurrentSessionAsync();
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

            var props = await _currentSession.TryGetMediaPropertiesAsync();
            var playbackInfo = _currentSession.GetPlaybackInfo();

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

            CurrentTrack = new NowPlayingTrack
            {
                Title = props.Title ?? string.Empty,
                Artist = props.Artist ?? string.Empty,
                AlbumTitle = props.AlbumTitle ?? string.Empty,
                AlbumArtwork = artwork,
                IsPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            };

            CurrentTrackChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Update track error: {ex.Message}");
            CurrentTrack = new NowPlayingTrack();
        }
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
