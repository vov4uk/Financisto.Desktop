using Avalonia.Controls;
using Avalonia.Input;
using Financisto.Desktop.Data;
using Financisto.Desktop.ViewModels.Dialogs;

namespace Financisto.Desktop.Views.Dialogs;

public partial class TransactionDialog : UserControl
{
    public TransactionDialog()
    {
        InitializeComponent();
    }

    private async void OnSubTransactionDoubleTapped(object sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: BaseTransactionDto item } && DataContext is TransactionDialogVM vm)
        {
            await vm.EditSubTransactionCommand.ExecuteAsync(item);
        }
    }
}
