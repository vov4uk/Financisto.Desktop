using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.Common.Utils;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Views.Dialogs;
using Financisto.Desktop.Wizards.RecipesWizard.ViewModel;
using Prism.Commands;

namespace Financisto.Desktop.ViewModels.Dialogs;

public class TransactionDialogVM : SubTransactionDialogVM
{
    private readonly IDialogWrapper dialogWrapper;
    private readonly IReadOnlyDictionary<int, long> accountBalances;
    private Common.IAsyncCommand _addSubTransactionCommand;
    private Common.IAsyncCommand _addSubTransferCommand;
    private DelegateCommand _clearLocationCommand;
    private DelegateCommand _clearPayeeCommand;
    private DelegateCommand _clearTagCommand;
    private DelegateCommand<BaseTransactionDto> _deleteSubTransactionCommand;
    private AsyncCommand<BaseTransactionDto> _editSubTransaction;
    private Common.IAsyncCommand _openRecipesDialogCommand;

    /// <param name="accountBalances">Account id → current balance (<c>IFinancistoDatabase.GetLastRunningBalancesAsync</c>), read right before
    /// the dialog opens; shown under the account combobox. Null shows no balance.</param>
    public TransactionDialogVM(
        TransactionDto transaction,
        IDialogWrapper dialogWrapper,
        IReadOnlyDictionary<int, long> accountBalances = null)
        : base(transaction)
    {
        this.dialogWrapper = dialogWrapper;
        this.accountBalances = accountBalances;
        Transaction.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TransactionDto.FromAccount))
            {
                OnPropertyChanged(nameof(FromAccountBalance));
                OnPropertyChanged(nameof(IsFromAccountBalanceNegative));

                // Android selectAccount: the difference is against the balance of the account selected now.
                if (Transaction.IsUpdateBalance && FromAccountBalanceValue is long balance)
                {
                    Transaction.CurrentBalance = balance;
                }
            }
            else if (e.PropertyName == nameof(TransactionDto.TemplateName))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(TransactionDto.BalanceDifference))
            {
                OnPropertyChanged(nameof(BalanceDifferenceText));
                OnPropertyChanged(nameof(IsBalanceDifferenceNegative));
                SaveCommand.NotifyCanExecuteChanged();
            }
        };
    }

    /// <summary>True for the account's "Balance" command: the amount is the new balance and only the difference is saved.</summary>
    public bool IsUpdateBalanceMode => Transaction.IsUpdateBalance;

    /// <summary>What the transaction records in update balance mode, formatted like the accounts grid (with a plus sign).</summary>
    public string BalanceDifferenceText => BlotterUtils.SetAmountText(Transaction.FromAccountCurrency, Transaction.BalanceDifference, true);

    public bool IsBalanceDifferenceNegative => Transaction.BalanceDifference < 0;

    /// <summary>The selected account's current balance, formatted like the accounts grid (AccountModel.AmountTitle).</summary>
    public string FromAccountBalance =>
        FromAccountBalanceValue is long balance ? BlotterUtils.SetAmountText(Transaction.FromAccountCurrency, balance, false) : null;

    public bool IsFromAccountBalanceNegative => FromAccountBalanceValue < 0;

    private long? FromAccountBalanceValue =>
        accountBalances != null && Transaction.FromAccount?.Id is int id ? accountBalances.GetValueOrDefault(id) : null;

    public Common.IAsyncCommand AddSubTransactionCommand => _addSubTransactionCommand ??= new AsyncCommand(() => ShowSubTransactionDialog(new TransactionDto(), true));
    public Common.IAsyncCommand AddSubTransferCommand => _addSubTransferCommand ??= new AsyncCommand(() => ShowSubTransferDialog(new TransferDto(), true));

    public DelegateCommand ClearLocationCommand => _clearLocationCommand ??= new DelegateCommand(() => { Transaction.LocationId = default; });

    public DelegateCommand ClearPayeeCommand => _clearPayeeCommand ??= new DelegateCommand(() => { Transaction.PayeeId = default; });

    public DelegateCommand ClearTagCommand => _clearTagCommand ??= new DelegateCommand(() => { Transaction.SelectedTags = new ObservableCollection<TagModel>(); });

    public DelegateCommand<BaseTransactionDto> DeleteSubTransactionCommand => _deleteSubTransactionCommand ??= new DelegateCommand<BaseTransactionDto>(tr =>
    {
        Transaction.SubTransactions.Remove(tr);
        Transaction.RecalculateUnSplitAmount();
    });

    public AsyncCommand<BaseTransactionDto> EditSubTransactionCommand => _editSubTransaction ??= new AsyncCommand<BaseTransactionDto>(EditSubTransaction);

    public Common.IAsyncCommand OpenRecipesDialogCommand => _openRecipesDialogCommand ??= new AsyncCommand(ShowRecipesDialog);

    // Only a split must be fully distributed among its parts; a regular transaction has no parts, so its UnsplitAmount is the whole amount.
    protected override bool CanSaveCommandExecute() =>
        Transaction.FromAccount != null
        // A template may have no amount (Android then opens the calculator when it is used), but needs a name to be found by in the list.
        && (Transaction.IsTemplate
            ? !string.IsNullOrWhiteSpace(Transaction.TemplateName)
            : Transaction.IsUpdateBalance ? Transaction.BalanceDifference != 0 : Transaction.FromAmount != 0)
        && base.CanSaveCommandExecute();

    private static void CopySubTransaction(TransactionDto original, TransactionDto modifiedCopy)
    {
        original.CategoryId = modifiedCopy.CategoryId;
        original.Category = DbManual.Category?.Find(x => x.Id == modifiedCopy.CategoryId)!;
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
        // The sign tells whether the money leaves (-) or comes into (+) the parent account.
        original.IsAmountNegative = modifiedCopy.IsAmountNegative;
        original.FromAmount = modifiedCopy.RealFromAmount;
        // A transfer to an account in another currency keeps its own incoming amount.
        original.ToAmount = Math.Abs(modifiedCopy.IsToAmountVisible ? modifiedCopy.ToAmount : modifiedCopy.FromAmount);
        original.Date = modifiedCopy.DateTime.Date;
        original.Time = modifiedCopy.DateTime;
    }

    /// <summary>Recipes wizard: parses a pasted receipt into split parts of this transaction.</summary>
    private async Task ShowRecipesDialog()
    {
        var vm = new RecipesVM(Transaction.RealFromAmount / 100.0);

        var output = await dialogWrapper.ShowWizardAsync(vm);

        if (output is List<TransactionDto> outputTransactions)
        {
            foreach (var item in outputTransactions)
            {
                item.Category = DbManual.Category?.Find(x => x.Id == item.CategoryId)!;
                Transaction.SubTransactions.Add(item);
            }
            Transaction.RecalculateUnSplitAmount();
            SaveCommand.NotifyCanExecuteChanged();
        }
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
        // Like Android's SplitTransferActivity, the parent account is always the "from" side of the dialog and
        // the sign says whether the money leaves it or comes into it; a new part takes over the unsplit amount.
        var workingCopy = new TransferDto()
        {
            IsSubTransaction = true,
            IsAmountNegative = Transaction.UnsplitAmount <= 0,
            FromAmount = Math.Abs(Transaction.UnsplitAmount),
            Date = Transaction.Date,
            Time = Transaction.Time,
        };
        if (!isNewItem)
        {
            CopySubTransfer(workingCopy, original);
        }

        workingCopy.FromAccountId = Transaction.FromAccountId;
        workingCopy.FromAccount = Transaction.FromAccount;

        var viewModel = new TransferDialogVM(workingCopy);

        var dialogResult = await dialogWrapper.ShowDialogAsync<TransferDialog>(viewModel, 440, 340, LocalizationService.Instance.transfer);

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
