using System.Collections.ObjectModel;
using System.Globalization;
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
        private ObservableCollection<LanguageItem> _availableLanguages = [];

        [ObservableProperty]
        private LanguageItem? _selectedLanguage;

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

        public bool CopyAlbumArtworkOnManualPost
        {
            get => _appSettingsService.CopyAlbumArtworkOnManualPost;
            set
            {
                if (_appSettingsService.CopyAlbumArtworkOnManualPost != value)
                {
                    _appSettingsService.CopyAlbumArtworkOnManualPost = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool OpenBrowserOnShareTimeout
        {
            get => _appSettingsService.OpenBrowserOnShareTimeout;
            set
            {
                if (_appSettingsService.OpenBrowserOnShareTimeout != value)
                {
                    _appSettingsService.OpenBrowserOnShareTimeout = value;
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

            InitializeLanguages();

            if (_navigationWindow is MainWindow mainWindow)
            {
                Topmost = mainWindow.Topmost;
            }

            OnPropertyChanged(nameof(AutoPost));
            OnPropertyChanged(nameof(PostAlbumArtwork));
            OnPropertyChanged(nameof(CopyAlbumArtworkOnManualPost));
            OnPropertyChanged(nameof(AutoCloseShareWindow));
            OnPropertyChanged(nameof(OpenBrowserOnShareTimeout));
            _appSettingsService.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AppSettingsService.AutoPost))
                    OnPropertyChanged(nameof(AutoPost));
                if (e.PropertyName == nameof(AppSettingsService.PostAlbumArtwork))
                    OnPropertyChanged(nameof(PostAlbumArtwork));
                if (e.PropertyName == nameof(AppSettingsService.CopyAlbumArtworkOnManualPost))
                    OnPropertyChanged(nameof(CopyAlbumArtworkOnManualPost));
                if (e.PropertyName == nameof(AppSettingsService.AutoCloseShareWindow))
                    OnPropertyChanged(nameof(AutoCloseShareWindow));
                if (e.PropertyName == nameof(AppSettingsService.OpenBrowserOnShareTimeout))
                    OnPropertyChanged(nameof(OpenBrowserOnShareTimeout));
            };

            _isInitialized = true;
        }

        private void InitializeLanguages()
        {
            var locService = LocalizationService.Instance;
            AvailableLanguages.Clear();

            foreach (var culture in locService.SupportedCultures)
            {
                var item = new LanguageItem(
                    culture.Name,
                    locService.GetCultureDisplayName(culture));
                AvailableLanguages.Add(item);

                if (culture.Name.Equals(locService.CurrentCulture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedLanguage = item;
                }
            }
        }

        partial void OnSelectedLanguageChanged(LanguageItem? value)
        {
            if (value != null && _isInitialized)
            {
                LocalizationService.Instance.SetCulture(value.CultureName);
            }
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

        [RelayCommand]
        private void OnChangeCopyAlbumArtworkOnManualPost(string parameter)
        {
            var newCopyAlbumArtworkOnManualPost = parameter == "copyalbumartonmanualpost_true";
            if (CopyAlbumArtworkOnManualPost == newCopyAlbumArtworkOnManualPost)
                return;

            CopyAlbumArtworkOnManualPost = newCopyAlbumArtworkOnManualPost;
        }

        [RelayCommand]
        private void OnChangeOpenBrowserOnShareTimeout(string parameter)
        {
            var newOpenBrowserOnShareTimeout = parameter == "openbrowseronsharetimeout_true";
            if (OpenBrowserOnShareTimeout == newOpenBrowserOnShareTimeout)
                return;

            OpenBrowserOnShareTimeout = newOpenBrowserOnShareTimeout;
        }
    }

    public record LanguageItem(string CultureName, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
