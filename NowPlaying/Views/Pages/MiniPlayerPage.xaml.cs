using System.Windows;
using NowPlaying.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace NowPlaying.Views.Pages;

public partial class MiniPlayerPage : INavigableView<DashboardViewModel>
{
    private const double MiniWidth = 640;
    private const double MiniHeight = 280;
    private const double NormalWidth = 640;
    private const double NormalHeight = 640;

    public DashboardViewModel ViewModel { get; }

    public MiniPlayerPage(DashboardViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ResizeToMini();
        await ViewModel.LoadedCommand.ExecuteAsync(null);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ResizeToNormal();
    }

    private void ResizeToMini()
    {
        var window = Window.GetWindow(this) ?? Application.Current.MainWindow;
        if (window != null)
        {
            window.Width = MiniWidth;
            window.Height = MiniHeight;
            window.MinWidth = MiniWidth;
            window.MinHeight = MiniHeight;
        }
    }

    private void ResizeToNormal()
    {
        var window = Application.Current.MainWindow;
        if (window != null)
        {
            window.MinWidth = 0;
            window.MinHeight = 0;
            window.Width = NormalWidth;
            window.Height = NormalHeight;
        }
    }
}
