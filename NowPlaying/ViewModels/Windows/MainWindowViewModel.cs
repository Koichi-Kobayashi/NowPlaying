using System.Collections.ObjectModel;
using NowPlaying.Services;
using Wpf.Ui.Controls;

namespace NowPlaying.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "Now Playing";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationViewItem()
            {
                Content = Loc.Get("Nav_Home"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage),
                Tag = "Nav_Home"
            },
            new NavigationViewItem()
            {
                Content = Loc.Get("Nav_MiniPlayer"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.MusicNote224 },
                TargetPageType = typeof(Views.Pages.MiniPlayerPage),
                Tag = "Nav_MiniPlayer"
            }
        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = Loc.Get("Nav_Settings"),
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage),
                Tag = "Nav_Settings"
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = Loc.Get("Nav_TrayHome"), Tag = "tray_home" }
        };

        public MainWindowViewModel()
        {
            LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            foreach (var item in MenuItems)
            {
                if (item is NavigationViewItem nvi && nvi.Tag is string key)
                    nvi.Content = Loc.Get(key);
            }
            foreach (var item in FooterMenuItems)
            {
                if (item is NavigationViewItem nvi && nvi.Tag is string key)
                    nvi.Content = Loc.Get(key);
            }
            foreach (var item in TrayMenuItems)
            {
                if (item is MenuItem mi && mi.Tag is "tray_home")
                    mi.Header = Loc.Get("Nav_TrayHome");
            }
        }
    }
}
