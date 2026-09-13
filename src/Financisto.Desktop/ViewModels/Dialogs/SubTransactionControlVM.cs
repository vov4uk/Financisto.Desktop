using CommunityToolkit.Mvvm.Input;
using Financisto.Desktop.Data;
using System.ComponentModel;
using System.Linq;

namespace Financisto.Desktop.ViewModels.Dialogs;

public partial class SubTransactionControlVM : DialogBaseVM
{
    private static readonly string[] TrackingProperties = { nameof(TransactionDto.FromAmount), nameof(TransactionDto.FromAccount) };

    public SubTransactionControlVM(TransactionDto transaction)
    {
        Transaction = transaction;
        Transaction.PropertyChanged += Transaction_PropertyChanged;
        Transaction.RecalculateRate();
    }

    public TransactionDto Transaction { get; }

    [RelayCommand]
    private void ChangeFromAmountSign() => Transaction.IsAmountNegative = !Transaction.IsAmountNegative;

    [RelayCommand]
    private void ClearCategory() => Transaction.CategoryId = 0;

    [RelayCommand]
    private void SplitCategory() => Transaction.CategoryId = -1;

    [RelayCommand]
    private void ClearFromAmount() => Transaction.FromAmount = 0;

    [RelayCommand]
    private void ClearNotes() => Transaction.Note = default;

    [RelayCommand]
    private void ClearOriginalFromAmount() => Transaction.OriginalFromAmount = 0;

    [RelayCommand]
    private void ClearProject() => Transaction.ProjectId = default;

    public override object OnRequestSave() => Transaction;

    protected override bool CanSaveCommandExecute() => !Transaction.IsSplitCategory || Transaction.UnsplitAmount == 0;

    private void Transaction_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (TrackingProperties.Contains(e.PropertyName))
        {
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
