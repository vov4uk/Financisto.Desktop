using Avalonia.Controls;
using Avalonia.Threading;

namespace Financisto.Desktop.Views.Dialogs;

public partial class TemplatesDialog : UserControl
{
    public TemplatesDialog()
    {
        InitializeComponent();

        // typing is the quickest way to find a template: start in the search box
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => SearchBox.Focus());
    }
}
