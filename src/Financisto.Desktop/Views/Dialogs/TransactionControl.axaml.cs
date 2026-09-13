using Avalonia.Controls;
using Avalonia.Input;
using Financisto.Desktop.Data;
using Financisto.Desktop.ViewModels.Dialogs;

namespace Financisto.Desktop.Views.Dialogs;

public partial class TransactionControl : UserControl
{
    public TransactionControl()
    {
        InitializeComponent();
    }

    private void OnSubTransactionDoubleTapped(object sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: BaseTransactionDto item } && DataContext is TransactionControlVM vm)
        {
            vm.EditSubTransactionCommand.Execute(item);
        }
    }
}
