using Avalonia.Controls;
using Financisto.Desktop.ViewModels;

namespace Financisto.Desktop.Views;

public partial class RelatorioPageView : UserControl
{
    public RelatorioPageView()
    {
        InitializeComponent();
        DataContext = new RelatorioPageViewModel();

    }
}