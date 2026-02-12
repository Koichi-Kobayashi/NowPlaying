using System.Windows;
using NowPlaying.Models;
using NowPlaying.Services;

namespace NowPlaying.ViewModels.Pages;

public partial class DashboardViewModel : ObservableObject
{
    private readonly NowPlayingService _nowPlayingService;
    private readonly ShareService _shareService;
    private readonly AppSettingsService _appSettingsService;
    private bool _hasReceivedFirstTrack;
    private string? _lastAutoPostedArtist;
    private string? _lastAutoPostedTitle;

    [ObservableProperty]
    private NowPlayingTrack _currentTrack = new();

    public DashboardViewModel(
        NowPlayingService nowPlayingService,
        ShareService shareService,
        AppSettingsService appSettingsService)
    {
        _nowPlayingService = nowPlayingService;
        _shareService = shareService;
        _appSettingsService = appSettingsService;

        _nowPlayingService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NowPlayingService.CurrentTrack))
            {
                CurrentTrack = _nowPlayingService.CurrentTrack;
            }
        };
        _nowPlayingService.CurrentTrackChanged += OnCurrentTrackChanged;
        CurrentTrack = _nowPlayingService.CurrentTrack;
    }

    private void OnCurrentTrackChanged(object? sender, EventArgs e)
    {
        if (!_appSettingsService.AutoPost || !_appSettingsService.HasSuccessfullyShared)
            return;

        var track = _nowPlayingService.CurrentTrack;
        if (track.IsEmpty)
            return;

        if (!_hasReceivedFirstTrack)
        {
            _hasReceivedFirstTrack = true;
            return;
        }

        if (_lastAutoPostedArtist == track.Artist && _lastAutoPostedTitle == track.Title)
            return;

        _lastAutoPostedArtist = track.Artist;
        _lastAutoPostedTitle = track.Title;
        _shareService.ShareViaWebView2(track);
    }

    [RelayCommand]
    private async Task LoadedAsync()
    {
        await _nowPlayingService.InitializeAsync();
        CurrentTrack = _nowPlayingService.CurrentTrack;
    }

    [RelayCommand]
    private void Refresh()
    {
        _nowPlayingService.Refresh();
        CurrentTrack = _nowPlayingService.CurrentTrack;
    }

    [RelayCommand]
    private void Share()
    {
        _shareService.ShareViaWebView2(CurrentTrack);
    }
}
