using System.Threading.Tasks;
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

    private async Task OnSubTransactionDoubleTapped(object sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: BaseTransactionDto item } && DataContext is TransactionControlVM vm)
        {
            await vm.EditSubTransactionCommand.ExecuteAsync(item);
        }
    }
}
