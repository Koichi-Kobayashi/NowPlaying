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
            AppVersion = $"UiDesktopApp1 - {GetAssemblyVersion()}";

            if (_navigationWindow is MainWindow mainWindow)
            {
                Topmost = mainWindow.Topmost;
            }

            OnPropertyChanged(nameof(AutoPost));
            _appSettingsService.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppSettingsService.AutoPost))
                    OnPropertyChanged(nameof(AutoPost));
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
    }
}
