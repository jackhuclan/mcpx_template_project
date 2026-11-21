using System.Windows;
using System.Windows.Controls;
using mcpx_template_project.ViewModels.Business;

namespace mcpx_template_project.Views.Pages.Business
{
    /// <summary>
    /// PageHome.xaml 的交互逻辑
    /// </summary>
    public partial class PageHome : Page
    {
        private readonly PageHomeViewModel _viewModel;

        public PageHome(PageHomeViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            _viewModel = viewModel;
            Unloaded += PageHome_Unloaded;
        }

        private void PageHome_Unloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.Stop();
        }
    }
}
