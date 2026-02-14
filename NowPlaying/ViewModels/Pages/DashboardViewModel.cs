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
    private bool _skipNextAutoPostAfterEnable;

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

        _appSettingsService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettingsService.AutoPost) && _appSettingsService.AutoPost)
            {
                _skipNextAutoPostAfterEnable = true;
            }
        };
    }

    private void OnCurrentTrackChanged(object? sender, EventArgs e)
    {
        if (!_appSettingsService.AutoPost || !_appSettingsService.HasSuccessfullyShared)
            return;

        var track = _nowPlayingService.CurrentTrack;
        if (track.IsEmpty)
            return;
        if (!track.IsPlaying)
            return;

        if (!_hasReceivedFirstTrack)
        {
            _hasReceivedFirstTrack = true;
            return;
        }

        if (_skipNextAutoPostAfterEnable)
        {
            _skipNextAutoPostAfterEnable = false;
            _lastAutoPostedArtist = track.Artist;
            _lastAutoPostedTitle = track.Title;
            return;
        }

        if (_lastAutoPostedArtist == track.Artist && _lastAutoPostedTitle == track.Title)
            return;

        _lastAutoPostedArtist = track.Artist;
        _lastAutoPostedTitle = track.Title;
        _shareService.ShareViaWebView2(track, isAutoPost: true);
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

    partial void OnCurrentTrackChanged(NowPlayingTrack value)
    {
        ShareCommand.NotifyCanExecuteChanged();
    }

    private bool CanShare()
    {
        return CurrentTrack.IsPlaying;
    }

    [RelayCommand(CanExecute = nameof(CanShare))]
    private void Share()
    {
        _shareService.ShareViaWebView2(CurrentTrack);
    }
}
