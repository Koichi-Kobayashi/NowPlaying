using NowPlaying.Services;
using NowPlaying.Views.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace NowPlaying.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;
        private readonly INavigationWindow _navigationWindow;
        private readonly AppSettingsService _appSettingsService;

        [ObservableProperty]
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        [ObservableProperty]
        private bool _topmost;

        public bool AutoPost
        {
            get => _appSettingsService.AutoPost;
            set
            {
                if (_appSettingsService.AutoPost != value)
                {
                    _appSettingsService.AutoPost = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool PostAlbumArtwork
        {
            get => _appSettingsService.PostAlbumArtwork;
            set
            {
                if (_appSettingsService.PostAlbumArtwork != value)
                {
                    _appSettingsService.PostAlbumArtwork = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool AutoCloseShareWindow
        {
            get => _appSettingsService.AutoCloseShareWindow;
            set
            {
                if (_appSettingsService.AutoCloseShareWindow != value)
                {
                    _appSettingsService.AutoCloseShareWindow = value;
                    OnPropertyChanged();
                }
            }
        }

        public SettingsViewModel(INavigationWindow navigationWindow, AppSettingsService appSettingsService)
        {
            _navigationWindow = navigationWindow;
            _appSettingsService = appSettingsService;
        }

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"{GetAssemblyVersion()}";

            if (_navigationWindow is MainWindow mainWindow)
            {
                Topmost = mainWindow.Topmost;
            }

            OnPropertyChanged(nameof(AutoPost));
            OnPropertyChanged(nameof(PostAlbumArtwork));
            OnPropertyChanged(nameof(AutoCloseShareWindow));
            _appSettingsService.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppSettingsService.AutoPost))
                    OnPropertyChanged(nameof(AutoPost));
                if (e.PropertyName == nameof(AppSettingsService.PostAlbumArtwork))
                    OnPropertyChanged(nameof(PostAlbumArtwork));
                if (e.PropertyName == nameof(AppSettingsService.AutoCloseShareWindow))
                    OnPropertyChanged(nameof(AutoCloseShareWindow));
            };

            _isInitialized = true;
        }

        private string GetAssemblyVersion()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;

                    break;
            }
        }

        [RelayCommand]
        private void OnChangeTopmost(string parameter)
        {
            var newTopmost = parameter == "topmost_true";
            if (Topmost == newTopmost)
                return;

            Topmost = newTopmost;
            if (_navigationWindow is MainWindow mainWindow)
            {
                mainWindow.Topmost = newTopmost;
            }
        }

        [RelayCommand]
        private void OnChangeAutoPost(string parameter)
        {
            var newAutoPost = parameter == "autopost_true";
            if (AutoPost == newAutoPost)
                return;

            AutoPost = newAutoPost;
        }

        [RelayCommand]
        private void OnChangePostAlbumArtwork(string parameter)
        {
            var newPostAlbumArtwork = parameter == "postalbumartwork_true";
            if (PostAlbumArtwork == newPostAlbumArtwork)
                return;

            PostAlbumArtwork = newPostAlbumArtwork;
        }

        [RelayCommand]
        private void OnChangeAutoCloseShareWindow(string parameter)
        {
            var newAutoCloseShareWindow = parameter == "autoclosesharewindow_true";
            if (AutoCloseShareWindow == newAutoCloseShareWindow)
                return;

            AutoCloseShareWindow = newAutoCloseShareWindow;
        }
    }
}
