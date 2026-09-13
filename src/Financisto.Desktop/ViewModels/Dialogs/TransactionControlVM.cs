using CommunityToolkit.Mvvm.Input;
using Financisto.Common.Entities;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Views.Dialogs;
using System;
using System.Threading.Tasks;

namespace Financisto.Desktop.ViewModels.Dialogs;

public partial class TransactionControlVM : SubTransactionControlVM
{
    private readonly IDialogWrapper _dialogWrapper;

    public TransactionControlVM(TransactionDto transaction, IDialogWrapper dialogWrapper)
        : base(transaction)
    {
        _dialogWrapper = dialogWrapper;
    }

    protected override bool CanSaveCommandExecute() => Transaction.FromAccount != null && Transaction.FromAmount != 0;

    [RelayCommand]
    private void ClearLocation() => Transaction.LocationId = default;

    [RelayCommand]
    private void ClearPayee() => Transaction.PayeeId = default;

    [RelayCommand]
    private Task AddSubTransaction() => ShowSubTransactionDialogAsync(new TransactionDto(), true);

    [RelayCommand]
    private void DeleteSubTransaction(BaseTransactionDto tr)
    {
        Transaction.SubTransactions.Remove(tr);
        Transaction.RecalculateUnSplitAmount();
    }

    [RelayCommand]
    private Task EditSubTransaction(BaseTransactionDto original)
    {
        // Editing a split-out transfer sub-item would need a TransferControl dialog, which
        // hasn't been ported yet (out of scope) — only TransactionDto sub-items are editable here.
        if (original is TransactionDto transaction)
        {
            return ShowSubTransactionDialogAsync(transaction, false);
        }

        return Task.CompletedTask;
    }

    private static void CopySubTransaction(TransactionDto original, TransactionDto modifiedCopy)
    {
        original.CategoryId = modifiedCopy.CategoryId;
        original.Category = DbManual.Category?.Find(x => x.Id == modifiedCopy.CategoryId);
        original.FromAmount = modifiedCopy.RealFromAmount;
        original.IsAmountNegative = modifiedCopy.IsAmountNegative;
        original.Note = modifiedCopy.Note;
        original.ProjectId = modifiedCopy.ProjectId;
    }

    private async Task ShowSubTransactionDialogAsync(TransactionDto original, bool isNewItem)
    {
        Transaction.RecalculateUnSplitAmount();
        var workingCopy = new TransactionDto { IsSubTransaction = true };
        if (isNewItem)
        {
            workingCopy.IsAmountNegative = Transaction.UnsplitAmount < 0;
            workingCopy.FromAmount = Math.Abs(Transaction.UnsplitAmount);
            workingCopy.ParentTransactionUnSplitAmount = Transaction.UnsplitAmount;
        }
        else
        {
            CopySubTransaction(workingCopy, original);
            workingCopy.IsAmountNegative = original.RealFromAmount <= 0;
            workingCopy.FromAmount = Math.Abs(original.FromAmount);
            workingCopy.ParentTransactionUnSplitAmount = Transaction.UnsplitAmount - Math.Abs(original.FromAmount);
        }

        var viewModel = new SubTransactionControlVM(workingCopy);

        var dialogResult = await _dialogWrapper.ShowDialogAsync<SubTransactionControl>(viewModel, 340, 400, "Sub Transaction");

        if (dialogResult is TransactionDto modifiedCopy)
        {
            CopySubTransaction(original, modifiedCopy);

            if (isNewItem) Transaction.SubTransactions.Add(original);
            Transaction.RecalculateUnSplitAmount();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
