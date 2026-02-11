using System.Windows;
using NowPlaying.Models;
using NowPlaying.Services;

namespace NowPlaying.ViewModels.Pages;

public partial class DashboardViewModel : ObservableObject
{
    private readonly NowPlayingService _nowPlayingService;
    private readonly ShareService _shareService;

    [ObservableProperty]
    private NowPlayingTrack _currentTrack = new();

    public DashboardViewModel(NowPlayingService nowPlayingService, ShareService shareService)
    {
        _nowPlayingService = nowPlayingService;
        _shareService = shareService;

        _nowPlayingService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NowPlayingService.CurrentTrack))
            {
                CurrentTrack = _nowPlayingService.CurrentTrack;
            }
        };
        CurrentTrack = _nowPlayingService.CurrentTrack;
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
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
        if (window != null)
        {
            _shareService.ShareViaWindows(CurrentTrack, window);
        }
        else
        {
            _shareService.FallbackToXUrl(CurrentTrack);
        }
    }
}
