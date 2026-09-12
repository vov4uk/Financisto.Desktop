using Avalonia.Controls;
using Financisto.Desktop.ViewModels;

namespace Financisto.Desktop.Views;

public partial class ReportsPageView : UserControl
{
    public ReportsPageView()
    {
        InitializeComponent();
        DataContext = new ReportsPageViewModel();

    }
}
