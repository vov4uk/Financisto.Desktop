using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.DataAccess.View;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Services;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.Views.Dialogs;

namespace Financisto.Desktop.ViewModels;

public partial class TransactionsPageViewModel : ViewModelBase
{
    private readonly IFinancistoDatabase _db;

    [ObservableProperty]
    private ObservableCollection<BlotterModel> _entities = new();

    [ObservableProperty]
    private bool _isLoading;

    public TransactionsPageViewModel(IFinancistoDatabase db)
    {
        _db = db;

        _ = RefreshDataAsync();
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (_db == null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            using var uow = _db.CreateUnitOfWork();
            var repo = uow.GetRepository<BlotterTransactions>();

            var items = await repo.FindManyAndProjectAsync(
                predicate: x => true,
                projection: x => new BlotterModel
                {
                    Id = x.Id,
                    FromAccountId = x.FromAccountId,
                    FromAccountTitle = x.FromAccountTitle,
                    ToAccountId = x.ToAccountId,
                    ToAccountTitle = x.ToAccountTitle,
                    FromAccountCurrencyId = x.FromAccountCurrencyId,
                    CategoryId = x.CategoryId,
                    CategoryTitle = x.CategoryTitle,
                    LocationId = x.LocationId,
                    Project = x.ProjectId > 0 ? DbManual.ProjectIds.GetValueOrDefault(x.ProjectId.Value) : default,
                    Location = x.Location,
                    Payee = x.Payee,
                    Note = x.Note,
                    FromAmount = x.FromAmount,
                    ToAmount = x.ToAmount,
                    Datetime = x.DateTime,
                    OriginalCurrencyId = x.OriginalCurrencyId,
                    OriginalFromAmount = x.OriginalFromAmount,
                    FromAccountBalance = x.FromAccountBalance,
                    ToAccountBalance = x.ToAccountBalance,
                    FromAccountCurrency = DbManual.CurrencyIds.GetValueOrDefault(x.FromAccountCurrencyId),
                    ToAccountCurrency = x.ToAccountCurrency == null ? default : DbManual.CurrencyIds.GetValueOrDefault(x.ToAccountCurrencyId.Value),
                    OriginalCurrency = x.OriginalCurrency == null ? default : DbManual.CurrencyIds.GetValueOrDefault(x.OriginalCurrencyId.Value)
                });

            Entities = new ObservableCollection<BlotterModel>(items.OrderByDescending(x => x.Datetime));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Add()
    {
        var transaction = await _db.GetOrCreateTransactionAsync(0);
        var subTransactions = await _db.GetSubTransactionsAsync(0);

        await OpenTransactionDialogAsync(transaction, subTransactions);
    }

    private async Task OpenTransactionDialogAsync(Transaction transaction, IEnumerable<Transaction> subTransactions)
    {
        var transactionDto = new TransactionDto(transaction, subTransactions);
        var dialogVm = new TransactionControlVM(transactionDto, AppServices.DialogWrapper);

        var result = await AppServices.DialogWrapper.ShowDialogAsync<TransactionControl>(dialogVm, 640, 580, LocalizationService.Instance.transaction);

        if (result is TransactionDto resultVm)
        {
            await SaveTransactionResultAsync(transaction, subTransactions, resultVm);
        }
    }

    private async Task SaveTransactionResultAsync(Transaction transaction, IEnumerable<Transaction> subTransactions, TransactionDto resultVm)
    {
        var resultTransactions = new List<Transaction>();

        MapperHelper.MapTransaction(resultVm, transaction);
        var totalFromAmountHomeCurrency = transaction.FromAmount;
        resultTransactions.Add(transaction);

        if (resultVm.SubTransactions?.Any() == true)
        {
            foreach (var subTransactionDto in resultVm.SubTransactions.OfType<TransactionDto>())
            {
                var subTransaction = await GetSubTransactionAsync(transaction, resultVm, subTransactionDto);

                // Set FromAmount in home currency
                if (resultVm.IsOriginalFromAmountVisible)
                {
                    var originalFromAmount = subTransactionDto.RealFromAmount;
                    subTransaction.FromAmount = (long)(originalFromAmount * resultVm.Rate);
                    subTransaction.OriginalFromAmount = originalFromAmount;
                    totalFromAmountHomeCurrency -= subTransaction.FromAmount;
                }

                resultTransactions.Add(subTransaction);
            }

            if (!resultVm.IsOriginalFromAmountVisible)
            {
                foreach (var subTransfer in resultVm.SubTransactions.OfType<TransferDto>())
                {
                    var subTransaction = await GetSubTransferAsync(transaction, resultVm, subTransfer);
                    resultTransactions.Add(subTransaction);
                }
            }

            // check if sum of all subtransactions == parentTransaction.FromAmount
            // if not - add the difference to the last transaction
            if (resultVm.IsOriginalFromAmountVisible && totalFromAmountHomeCurrency != 0)
            {
                resultTransactions[^1].FromAmount += totalFromAmountHomeCurrency;
            }
        }

        await _db.InsertOrUpdateAsync(resultTransactions);

        await ProcessDeletedTransactionsAsync(subTransactions, resultTransactions);

        await _db.RebuildAccountBalanceAsync(transaction.FromAccountId);
        var toAccounts = resultTransactions.Select(x => x.ToAccountId).Where(x => x > 0).Distinct().ToList();
        foreach (var account in toAccounts)
        {
            await _db.RebuildAccountBalanceAsync(account);
        }

        await RefreshDataAsync();
    }

    private async Task ProcessDeletedTransactionsAsync(IEnumerable<Transaction> subTransactions, List<Transaction> resultTransactions)
    {
        var transactionIds = resultTransactions.Select(x => x.Id).Distinct().ToList();
        var deletedSubTransactionIds = subTransactions.Select(x => x.Id).Where(x => !transactionIds.Contains(x));
        foreach (var id in deletedSubTransactionIds)
        {
            await DeleteTransactionAsync(id);
        }
    }

    private async Task DeleteTransactionAsync(int id)
    {
        using var uow = _db.CreateUnitOfWork();
        var repo = uow.GetRepository<Transaction>();
        var transaction = await repo.FindByAsync(x => x.Id == id);

        await repo.DeleteAsync(transaction);
        await uow.SaveChangesAsync();
    }

    private async Task<Transaction> GetSubTransactionAsync(Transaction transaction, TransactionDto resultVm, TransactionDto subTransactionDto)
    {
        var subTransaction = await _db.GetOrCreateAsync<Transaction>(subTransactionDto.Id);
        subTransactionDto.Date = resultVm.Date;
        subTransactionDto.Time = resultVm.Time;
        MapperHelper.MapTransaction(subTransactionDto, subTransaction);
        subTransaction.Parent = transaction;
        subTransaction.FromAccountId = transaction.FromAccountId;
        subTransaction.OriginalCurrencyId = transaction.OriginalCurrencyId ?? transaction.FromAccount.CurrencyId;
        subTransaction.Category = default;
        return subTransaction;
    }

    private async Task<Transaction> GetSubTransferAsync(Transaction transaction, TransactionDto resultVm, TransferDto subTransfer)
    {
        var subTransaction = await _db.GetOrCreateAsync<Transaction>(subTransfer.Id);
        subTransfer.Date = resultVm.Date;
        subTransfer.Time = resultVm.Time;
        MapperHelper.MapTransfer(subTransfer, subTransaction);
        subTransaction.Parent = transaction;
        subTransaction.FromAccountId = transaction.FromAccountId;
        return subTransaction;
    }

    [RelayCommand]
    private void AddTransfer()
    {
    }

    [RelayCommand]
    private void Edit()
    {
    }

    [RelayCommand]
    private void Duplicate()
    {
    }

    [RelayCommand]
    private void Delete()
    {
    }
}
