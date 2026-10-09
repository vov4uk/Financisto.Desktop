using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common.Localization;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Data;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.Views.Dialogs;

namespace Financisto.Desktop.Helpers
{
    /// <summary>
    /// Opens the transaction and transfer dialogs and saves what they return, for every page that creates transactions
    /// (the blotter, and the accounts page's context menu). It rebuilds the balances of the accounts it touched; the caller refreshes its page.
    /// </summary>
    internal sealed class TransactionEditor
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IFinancistoDatabase db;
        private readonly IDialogWrapper dialogWrapper;

        public TransactionEditor(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
        {
            this.db = db;
            this.dialogWrapper = dialogWrapper;
        }

        /// <summary>Shows the transfer dialog for <paramref name="transfer"/> and saves it; false when cancelled.</summary>
        public async Task<bool> EditTransferAsync(Transaction transfer)
        {
            TransferDialogVM dialogVm = new TransferDialogVM(new TransferDto(transfer), await db.GetLastRunningBalancesAsync());

            var result = await dialogWrapper.ShowDialogAsync<TransferDialog>(dialogVm, 480, 440, LocalizationService.Instance.transfer);

            if (result is not TransferDto output)
            {
                return false;
            }

            MapperHelper.MapTransfer(output, transfer);
            await db.InsertOrUpdateAsync(new[] { transfer });

            await db.RebuildAccountBalanceAsync(transfer.FromAccountId);
            await db.RebuildAccountBalanceAsync(transfer.ToAccountId);
            return true;
        }

        /// <summary>
        /// Shows the transaction dialog and saves it; false when cancelled.
        /// </summary>
        /// <param name="currentBalance">Opens the dialog in update balance mode (Android's "Balance" command): the amount starts at this
        /// balance and the saved transaction is the difference to it.</param>
        public async Task<bool> EditTransactionAsync(Transaction transaction, IEnumerable<Transaction> subTransactions, long? currentBalance = null)
        {
            var transactionDto = new TransactionDto(transaction, subTransactions);
            if (currentBalance is long balance)
            {
                transactionDto.StartBalanceUpdate(balance);
            }

            TransactionDialogVM dialogVm = new TransactionDialogVM(transactionDto, dialogWrapper, await db.GetLastRunningBalancesAsync());

            var title = currentBalance == null ? LocalizationService.Instance.transaction : LocalizationService.Instance.update_balance;
            var result = await dialogWrapper.ShowDialogAsync<TransactionDialog>(dialogVm, 640, 440, title);
            if (result is not TransactionDto resultVm)
            {
                return false;
            }

            if (resultVm.IsUpdateBalance)
            {
                resultVm.ApplyBalanceDifference();
            }

            await SaveTransactionResult(transaction, subTransactions, resultVm);
            return true;
        }

        /// <summary>Deletes a transaction together with its split parts (no balance rebuild).</summary>
        public async Task DeleteTransactionAsync(int id)
        {
            // id 0 would match every top-level transaction through parent_id below.
            if (id <= 0)
            {
                return;
            }

            Logger.Info($"On Transaction delete id : {id}");
            using (var uow = db.CreateUnitOfWork())
            {
                // Split parts point to their parent via parent_id; delete them together so none are left orphaned.
                await uow.GetRepository<Transaction>().DeleteAsync(x => x.Id == id || x.ParentId == id);
                await uow.SaveChangesAsync();
            }
        }

        private async Task SaveTransactionResult(Transaction transaction, IEnumerable<Transaction> subTransactions, TransactionDto resultVm)
        {
            var resultTransactions = new List<Transaction>();

            // Accounts the transaction touched before the edit also need their balance rebuilt,
            // e.g. the other account of a split transfer that was removed or moved.
            var previousAccounts = subTransactions
                .SelectMany(x => new[] { x.FromAccountId, x.ToAccountId })
                .Append(transaction.FromAccountId)
                .ToList();

            MapperHelper.MapTransaction(resultVm, transaction);
            long totalFromAmountHomeCurrency = transaction.FromAmount;
            resultTransactions.Add(transaction);
            if (resultVm.SubTransactions?.Any() == true)
            {
                foreach (var subTransactionDto in resultVm.SubTransactions.OfType<TransactionDto>())
                {
                    Transaction subTransaction = await GetSubTransaction(transaction, resultVm, subTransactionDto);

                    //Set FromAmount in home currency
                    if (resultVm.IsOriginalFromAmountVisible)
                    {
                        var originalFromAmount = (subTransactionDto).RealFromAmount;
                        subTransaction.FromAmount = (long)(originalFromAmount * resultVm.Rate);
                        subTransaction.OriginalFromAmount = originalFromAmount;
                        totalFromAmountHomeCurrency -= subTransaction.FromAmount;
                    }

                    resultTransactions.Add(subTransaction);
                }

                if (!resultVm.IsOriginalFromAmountVisible)
                {
                    foreach (var subTranfer in resultVm.SubTransactions.OfType<TransferDto>())
                    {
                        Transaction subTransaction = await GetSubTransfer(transaction, resultVm, subTranfer);
                        resultTransactions.Add(subTransaction);
                    }
                }

                // check if sum of all subtransaction == parentTransaction.FromAmount
                // if not - add diference to last transaction
                if (resultVm.IsOriginalFromAmountVisible && totalFromAmountHomeCurrency != 0)
                {
                    resultTransactions[resultTransactions.Count - 1].FromAmount += totalFromAmountHomeCurrency;
                }
            }

            await db.InsertOrUpdateAsync(resultTransactions);

            await ProcessDeletedTransactions(subTransactions, resultTransactions);

            // An incoming split transfer has the other account on its "from" side, so rebuild both sides.
            var accounts = resultTransactions
                .SelectMany(x => new[] { x.FromAccountId, x.ToAccountId })
                .Concat(previousAccounts)
                .Where(x => x > 0)
                .Distinct()
                .ToList();
            foreach (var account in accounts)
            {
                await db.RebuildAccountBalanceAsync(account);
            }
        }

        private async Task ProcessDeletedTransactions(IEnumerable<Transaction> subTransactions, List<Transaction> resultTransactions)
        {
            var transactionsIds = resultTransactions.Select(x => x.Id).Distinct().ToList();
            var deletedSubTransaction = subTransactions.Select(x => x.Id).Where(x => x > 0 && !transactionsIds.Contains(x));
            foreach (var t in deletedSubTransaction)
            {
                await DeleteTransactionAsync(t);
            }
        }

        private async Task<Transaction> GetSubTransfer(Transaction transaction, TransactionDto resultVm, TransferDto subTranfer)
        {
            var subTransaction = await db.GetOrCreateAsync<Transaction>(subTranfer.Id);

            subTranfer.Date = resultVm.Date;
            subTranfer.Time = resultVm.Time;
            // The parent account's side of the part follows the parent's account;
            // MapTransfer then stores it as the "from" or the "to" side depending on the direction.
            subTranfer.FromAccountId = transaction.FromAccountId;
            subTranfer.FromAccount = resultVm.FromAccount;
            MapperHelper.MapTransfer(subTranfer, subTransaction);
            subTransaction.Parent = transaction;
            subTransaction.ParentAccountId = transaction.FromAccountId;
            return subTransaction;
        }

        private async Task<Transaction> GetSubTransaction(Transaction transaction, TransactionDto resultVm, TransactionDto subTransactionDto)
        {
            var subTransaction = await db.GetOrCreateAsync<Transaction>(subTransactionDto.Id);
            subTransactionDto.Date = resultVm.Date;
            subTransactionDto.Time = resultVm.Time;
            MapperHelper.MapTransaction(subTransactionDto, subTransaction);
            subTransaction.Parent = transaction;
            subTransaction.FromAccountId = transaction.FromAccountId;
            // Same as Android DatabaseAdapter.insertSplits: split parts carry the parent's account.
            subTransaction.ParentAccountId = transaction.FromAccountId;
            subTransaction.OriginalCurrencyId = transaction.OriginalCurrencyId ?? transaction.FromAccount.CurrencyId;
            subTransaction.Category = default;
            return subTransaction;
        }
    }
}
