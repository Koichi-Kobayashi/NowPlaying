using System.Windows;
using System.Windows.Media;
using NowPlaying.Models;
using NowPlaying.Services;
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
        private const double MiniWidth = 640;
        private const double MiniHeight = 280;
        private const double NormalWidth = 640;
        private const double NormalHeight = 640;
        public MainWindowViewModel ViewModel { get; }

        private readonly WindowStateService _windowStateService;
        private bool _isRestoringState;

        public MainWindow(
            MainWindowViewModel viewModel,
            INavigationViewPageProvider navigationViewPageProvider,
            INavigationService navigationService,
            WindowStateService windowStateService
        )
        {
            ViewModel = viewModel;
            DataContext = this;
            _windowStateService = windowStateService;

            SystemThemeWatcher.Watch(this);

            InitializeComponent();
            SetPageService(navigationViewPageProvider);

            navigationService.SetNavigationControl(RootNavigation);

            RootNavigation.Navigated += RootNavigation_OnNavigated;
            Closing += MainWindow_Closing;
        }

        /// <summary>
        /// 保存されたウィンドウ状態を復元し、表示するページの型を返します。
        /// </summary>
        public Type RestoreState()
        {
            var state = _windowStateService.Load();
            if (state == null)
                return typeof(DashboardPage);

            _isRestoringState = true;
            WindowStartupLocation = WindowStartupLocation.Manual;

            Left = state.Left;
            Top = state.Top;
            Width = state.Width;
            Height = state.Height;

            if (state.IsMiniPlayer)
            {
                MinWidth = MiniWidth;
                MinHeight = MiniHeight;
                return typeof(MiniPlayerPage);
            }
            else
            {
                MinWidth = 0;
                MinHeight = 0;
                return typeof(DashboardPage);
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var isMiniPlayer = FindChild<MiniPlayerPage>(RootNavigation) != null;
            var state = new Models.WindowState
            {
                Width = Width,
                Height = Height,
                Left = Left,
                Top = Top,
                IsMiniPlayer = isMiniPlayer
            };
            _windowStateService.Save(state);
        }

        private void RootNavigation_OnNavigated(object sender, RoutedEventArgs e)
        {
            // ナビゲーション後にページがツリーに追加されるのを待つ
            Dispatcher.BeginInvoke(() =>
            {
                // 復元中はサイズを変更しない（保存された値を維持）
                if (_isRestoringState)
                {
                    _isRestoringState = false;
                    return;
                }

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
