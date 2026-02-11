using NowPlaying.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace NowPlaying.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage(DashboardViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
            Loaded += async (_, _) => await ViewModel.LoadedCommand.ExecuteAsync(null);
        }
    }
}
