using System.Windows;
using System.Windows.Media;
using NowPlaying.ViewModels.Windows;
using NowPlaying.Views.Pages;
using Wpf.Ui;
using Wpf.Ui.Abstractions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace NowPlaying.Views.Windows
{
    public partial class MainWindow : INavigationWindow
    {
        private const double MiniWidth = 520;
        private const double MiniHeight = 340;
        private const double NormalWidth = 640;
        private const double NormalHeight = 640;
        public MainWindowViewModel ViewModel { get; }

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService
        )
        {
            ViewModel = viewModel;
            DataContext = this;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);

            RootNavigation.Navigated += RootNavigation_OnNavigated;
        }

        private void RootNavigation_OnNavigated(object sender, RoutedEventArgs e)
        {
            // ナビゲーション後にページがツリーに追加されるのを待つ
            Dispatcher.BeginInvoke(() =>
            {
                var isMiniPlayer = FindChild<MiniPlayerPage>(RootNavigation) != null;

                if (isMiniPlayer)
                {
                    Width = MiniWidth;
                Height = MiniHeight;
                    MinWidth = MiniWidth;
                    MinHeight = MiniHeight;
                }
                else
                {
                    MinWidth = 0;
                    MinHeight = 0;
                    Width = NormalWidth;
                    Height = NormalHeight;
                }
            });
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T found)
                    return found;
                if (FindChild<T>(child) is T nested)
                    return nested;
            }
            return null;
        }

        #region INavigationWindow methods

        public INavigationView GetNavigation() => RootNavigation;

        public bool Navigate(Type pageType) => RootNavigation.Navigate(pageType);

        public void SetPageService(INavigationViewPageProvider navigationViewPageProvider) => RootNavigation.SetPageProviderService(navigationViewPageProvider);

        public void ShowWindow() => Show();

        public void CloseWindow() => Close();

        #endregion INavigationWindow methods

        /// <summary>
        /// Raises the closed event.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Make sure that closing this window will begin the process of closing the application.
            Application.Current.Shutdown();
        }

        INavigationView INavigationWindow.GetNavigation()
        {
            throw new NotImplementedException();
        }

        public void SetServiceProvider(IServiceProvider serviceProvider)
        {
            throw new NotImplementedException();
        }
    }
}
