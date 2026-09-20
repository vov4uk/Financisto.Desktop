using System;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Views.Dialogs;
using Prism.Commands;

namespace Financisto.Desktop.ViewModels.Dialogs;

public class TransactionDialogVM : SubTransactionDialogVM
{
    private readonly IDialogWrapper dialogWrapper;
    private DelegateCommand _addSubTransactionCommand;
    private DelegateCommand _addSubTransferCommand;
    private DelegateCommand _clearLocationCommand;
    private DelegateCommand _clearPayeeCommand;
    private DelegateCommand<BaseTransactionDto> _deleteSubTransactionCommand;
    private AsyncCommand<BaseTransactionDto> _editSubTransaction;

    public TransactionDialogVM(
        TransactionDto transaction,
        IDialogWrapper dialogWrapper)
        : base(transaction)
    {
        this.dialogWrapper = dialogWrapper;
    }

    public DelegateCommand AddSubTransactionCommand => _addSubTransactionCommand ??= new DelegateCommand(() => { ShowSubTransactionDialog(new TransactionDto(), true); });
    public DelegateCommand AddSubTransferCommand => _addSubTransferCommand ??= new DelegateCommand(() => { ShowSubTransferDialog(new TransferDto(), true); });

    public DelegateCommand ClearLocationCommand => _clearLocationCommand ??= new DelegateCommand(() => { Transaction.LocationId = default; });

    public DelegateCommand ClearPayeeCommand => _clearPayeeCommand ??= new DelegateCommand(() => { Transaction.PayeeId = default; });

    public DelegateCommand<BaseTransactionDto> DeleteSubTransactionCommand => _deleteSubTransactionCommand ??= new DelegateCommand<BaseTransactionDto>(tr =>
    {
        Transaction.SubTransactions.Remove(tr);
        Transaction.RecalculateUnSplitAmount();
    });

    public AsyncCommand<BaseTransactionDto> EditSubTransactionCommand => _editSubTransaction ??= new AsyncCommand<BaseTransactionDto>(EditSubTransaction);

    protected override bool CanSaveCommandExecute() => Transaction.FromAccount != null && Transaction.FromAmount != 0 && Transaction.UnsplitAmount == 0;

    private static void CopySubTransaction(TransactionDto original, TransactionDto modifiedCopy)
    {
        original.CategoryId = modifiedCopy.CategoryId;
        original.Category = DbManual.Category?.Find(x => x.Id == modifiedCopy.CategoryId);
        original.FromAmount = modifiedCopy.RealFromAmount;
        original.IsAmountNegative = modifiedCopy.IsAmountNegative;
        original.Note = modifiedCopy.Note;
        original.ProjectId = modifiedCopy.ProjectId;
    }

    private static void CopySubTransfer(TransferDto original, TransferDto modifiedCopy)
    {
        original.Id = modifiedCopy.Id;
        original.FromAccountId = modifiedCopy.FromAccountId;
        original.FromAccount = modifiedCopy.FromAccount;
        original.ToAccountId = modifiedCopy.ToAccountId;
        original.ToAccount = modifiedCopy.ToAccount;
        original.Note = modifiedCopy.Note;
        original.FromAmount = modifiedCopy.RealFromAmount;
        original.ToAmount = Math.Abs(modifiedCopy.FromAmount);
        original.Date = modifiedCopy.DateTime.Date;
        original.Time = modifiedCopy.DateTime;
    }

    private async Task EditSubTransaction(BaseTransactionDto original)
    {
        var transaction = original as TransactionDto;
        if (transaction != null)
        {
            await ShowSubTransactionDialog(transaction, false);
            return;
        }

        var transfer = original as TransferDto;
        if (transfer != null)
        {
            await ShowSubTransferDialog(transfer, false);
        }
    }

    private async Task ShowSubTransferDialog(TransferDto original, bool isNewItem)
    {
        if (Transaction.IsOriginalFromAmountVisible)
        {
            await this.dialogWrapper.ShowMessageBoxAsync(LocalizationService.Instance.split_transfers_currency_not_supported, LocalizationService.Instance.not_supported);
            return;
        }

        Transaction.RecalculateUnSplitAmount();
        var workingCopy = new TransferDto()
        {
            FromAccountId = Transaction.FromAccountId,
            IsSubTransaction = true,
            FromAmount = Transaction.UnsplitAmount,
            Date = Transaction.Date,
            Time = Transaction.Time,
        };
        if (!isNewItem)
        {
            CopySubTransfer(workingCopy, original);
        }

        var viewModel = new TransferDialogVM(workingCopy);

        var dialogResult = await dialogWrapper.ShowDialogAsync<TransferDialog>(viewModel, 385, 340, LocalizationService.Instance.transfer);

        var modifiedCopy = dialogResult as TransferDto;
        if (modifiedCopy != null)
        {
            CopySubTransfer(original, modifiedCopy);

            if (isNewItem) Transaction.SubTransactions.Add(original);
            Transaction.RecalculateUnSplitAmount();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task ShowSubTransactionDialog(TransactionDto original, bool isNewItem)
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

        var viewModel = new SubTransactionDialogVM(workingCopy);

        var dialogResult = await dialogWrapper.ShowDialogAsync<SubTransactionDialog>(viewModel, 340, 340, LocalizationService.Instance.sub_transaction);

        var modifiedCopy = dialogResult as TransactionDto;
        if (modifiedCopy != null)
        {
            CopySubTransaction(original, modifiedCopy);

            if (isNewItem) Transaction.SubTransactions.Add(original);
            Transaction.RecalculateUnSplitAmount();
            SaveCommand.NotifyCanExecuteChanged();
        }
    }
}
