using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.Converters;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.Views.Dialogs;

namespace Financisto.Desktop.ViewModels.Pages
{
    [ExcludeFromCodeCoverage]
    public class AccountsPageVM : EntityBaseVM<AccountModel>
    {
        private IAsyncCommand _addTransactionCommand;
        private IAsyncCommand _addTransferCommand;
        private IAsyncCommand _closeAccountCommand;
        private IAsyncCommand _deleteOldTransactionsCommand;
        private IAsyncCommand _reopenAccountCommand;
        private IAsyncCommand _showBlotterCommand;
        private IAsyncCommand _showInfoCommand;
        private IAsyncCommand _transferCurrentBalanceCommand;
        private IAsyncCommand _updateBalanceCommand;
        private TransactionEditor _editor;

        public AccountsPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        /// <summary>
        /// Shows the blotter with only this account's transactions (the account id). The shell sets it, as it owns the pages;
        /// without it the Blotter command does nothing.
        /// </summary>
        public Func<int, Task> ShowAccountTransactions { get; set; }

        // The account's context menu: the same commands as Android's account quick actions (AccountListFragment.prepareAccountActionGrid),
        // all acting on the selected account. Edit and Delete account are EditCommand and DeleteCommand.
        public IAsyncCommand ShowInfoCommand => _showInfoCommand ??= new AsyncCommand(() => OnShowInfo(SelectedValue), HasSelection);

        public IAsyncCommand ShowBlotterCommand => _showBlotterCommand ??= new AsyncCommand(() => OnShowBlotter(SelectedValue), HasSelection);

        public IAsyncCommand AddTransactionCommand => _addTransactionCommand ??= new AsyncCommand(() => OnAddTransaction(SelectedValue), HasSelection);

        public IAsyncCommand AddTransferCommand => _addTransferCommand ??= new AsyncCommand(() => OnAddTransfer(SelectedValue), HasSelection);

        /// <summary>Android's "Balance": a transaction that sets the account to the balance you enter.</summary>
        public IAsyncCommand UpdateBalanceCommand => _updateBalanceCommand ??= new AsyncCommand(() => OnUpdateBalance(SelectedValue), HasSelection);

        public IAsyncCommand DeleteOldTransactionsCommand =>
            _deleteOldTransactionsCommand ??= new AsyncCommand(() => OnDeleteOldTransactions(SelectedValue), HasSelection);

        public IAsyncCommand CloseAccountCommand =>
            _closeAccountCommand ??= new AsyncCommand(() => OnCloseAccount(SelectedValue), () => SelectedValue?.IsActive == true);

        public IAsyncCommand ReopenAccountCommand =>
            _reopenAccountCommand ??= new AsyncCommand(() => SetActiveAsync(SelectedValue, true), () => SelectedValue?.IsActive == false);

        public IAsyncCommand TransferCurrentBalanceCommand =>
            _transferCurrentBalanceCommand ??= new AsyncCommand(() => OnTransferCurrentBalance(SelectedValue), HasSelection);

        private TransactionEditor Editor => _editor ??= new TransactionEditor(db, dialogWrapper);

        protected override Task OnAdd() => OpenAccountDialogAsync(0);

        protected override Task OnEdit(AccountModel item) => OpenAccountDialogAsync(item.Id ?? 0);

        // Android's deleteAccount: the account and its transactions are removed (Close account is the soft way).
        protected override async Task OnDelete(AccountModel item)
        {
            if (!await dialogWrapper.ShowMessageBoxAsync(
                    LocalizationService.Instance.delete_account_confirm,
                    LocalizationService.Instance.delete_account,
                    yesNoButtons: true))
                return;

            await db.DeleteAccountAsync(item.Id ?? 0);
            await ReloadAsync();
        }

        protected override void OnSelectedValueChanged()
        {
            base.OnSelectedValueChanged();
            ShowInfoCommand.RaiseCanExecuteChanged();
            ShowBlotterCommand.RaiseCanExecuteChanged();
            AddTransactionCommand.RaiseCanExecuteChanged();
            AddTransferCommand.RaiseCanExecuteChanged();
            UpdateBalanceCommand.RaiseCanExecuteChanged();
            DeleteOldTransactionsCommand.RaiseCanExecuteChanged();
            CloseAccountCommand.RaiseCanExecuteChanged();
            ReopenAccountCommand.RaiseCanExecuteChanged();
            TransferCurrentBalanceCommand.RaiseCanExecuteChanged();
        }

        protected override async Task RefreshData()
        {
            using var uow = db.CreateUnitOfWork();
            var accountRepo = uow.GetRepository<Account>();
            var items = await accountRepo.FindManyAndProjectAsync(
                predicate: x => true,
                projection: acc => new AccountModel(acc),
                includes: x => x.Currency);

            Entities = new ObservableCollection<AccountModel>(
                items.OrderByDescending(x => x.IsActive).ThenBy(x => x.SortOrder));
        }

        private async Task OpenAccountDialogAsync(int id)
        {
            var isNew = id == 0;
            Account account = await db.GetOrCreateAsync<Account>(id);

            AccountDto dto;
            if (isNew)
            {
                dto = new AccountDto
                {
                    Type = "CASH",
                    IsActive = true,
                    IsIncludeIntoTotals = true,
                };
            }
            else
            {
                dto = new AccountDto(account);
            }

            var vm = new AccountDialogVM(dto, isNew);
            var result = await dialogWrapper.ShowDialogAsync<AccountDialog>(
                vm, 580, 560, LocalizationService.Instance["account"]);

            if (result is not AccountDto updated)
                return;

            ApplyDto(account, updated);
            await db.InsertOrUpdateAsync(new[] { account });

            if (isNew && updated.OpeningAmount != 0)
            {
                var t = new Transaction
                {
                    Id = 0, // new entity; the default -1 would be saved as an update
                    FromAccountId = account.Id,
                    CategoryId = 0,
                    FromAmount = updated.OpeningAmount,
                    DateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                await db.InsertOrUpdateAsync(new[] { t });
                await db.RebuildAccountBalanceAsync(account.Id);
            }

            await ReloadAsync();
        }

        private bool HasSelection() => SelectedValue != null;

        /// <summary>After a change that can affect the accounts' balances or titles: refresh the cached account lists, then the grid.</summary>
        private async Task ReloadAsync()
        {
            DbManual.ResetManuals(nameof(DbManual.Account));
            await DbManual.SetupAsync(db);
            await RefreshData();
        }

        // Android's "Info": the read-only details, with an Edit button.
        private async Task OnShowInfo(AccountModel item)
        {
            Account account;
            using (var uow = db.CreateUnitOfWork())
            {
                account = await uow.GetRepository<Account>().FindByAsync(x => x.Id == item.Id, x => x.Currency);
            }

            if (account == null)
                return;

            var result = await dialogWrapper.ShowDialogAsync<AccountInfoDialog>(
                new AccountInfoDialogVM(account), 400, 420, LocalizationService.Instance.info);

            if (result is int accountId)
                await OpenAccountDialogAsync(accountId);
        }

        // Android's "Blotter": the account's transactions.
        private async Task OnShowBlotter(AccountModel item)
        {
            if (ShowAccountTransactions != null)
                await ShowAccountTransactions(item.Id ?? 0);
        }

        // Android's "Transaction": the new transaction form with this account selected.
        private async Task OnAddTransaction(AccountModel item)
        {
            var transaction = await db.GetOrCreateTransactionAsync(0);
            transaction.FromAccountId = item.Id ?? 0;

            if (await Editor.EditTransactionAsync(transaction, Array.Empty<Transaction>()))
                await ReloadAsync();
        }

        // Android's "Transfer": the new transfer form with this account as the source.
        private async Task OnAddTransfer(AccountModel item)
        {
            var transfer = await db.GetOrCreateTransactionAsync(0);
            transfer.FromAccountId = item.Id ?? 0;

            if (await Editor.EditTransferAsync(transfer))
                await ReloadAsync();
        }

        // Android's "Transfer current balance": the transfer form with the account's whole balance as the amount.
        private async Task OnTransferCurrentBalance(AccountModel item)
        {
            var account = await db.GetOrCreateAsync<Account>(item.Id ?? 0);
            if (account == null)
                return;

            var transfer = await db.GetOrCreateTransactionAsync(0);
            transfer.FromAccountId = account.Id;
            transfer.FromAmount = Math.Abs(account.TotalAmount);

            if (await Editor.EditTransferAsync(transfer))
                await ReloadAsync();
        }

        // Android's "Balance": the transaction form in update balance mode, starting at the account's current balance.
        private async Task OnUpdateBalance(AccountModel item)
        {
            var account = await db.GetOrCreateAsync<Account>(item.Id ?? 0);
            if (account == null)
                return;

            var transaction = await db.GetOrCreateTransactionAsync(0);
            transaction.FromAccountId = account.Id;

            if (await Editor.EditTransactionAsync(transaction, Array.Empty<Transaction>(), account.TotalAmount))
                await ReloadAsync();
        }

        // Android's "Delete old transactions": asks for the date, confirms, then replaces everything up to it with one balance transaction.
        private async Task OnDeleteOldTransactions(AccountModel item)
        {
            var dialogVm = new PurgeAccountDialogVM(item.Title, DateTime.Now.AddYears(-1).AddDays(-1));
            var result = await dialogWrapper.ShowDialogAsync<PurgeAccountDialog>(
                dialogVm, 330, 420, LocalizationService.Instance.delete_old_transactions);

            if (result is not DateTime date)
                return;

            var message = string.Format(LocalizationService.Instance.purge_account_confirm_message, item.Title, date.ToString("D"));
            if (!await dialogWrapper.ShowMessageBoxAsync(message, LocalizationService.Instance.confirm, yesNoButtons: true))
                return;

            await db.PurgeAccountAsync(item.Id ?? 0, UnixTimeConverter.ConvertBack(date), LocalizationService.Instance.purge_account_payee);

            DbManual.ResetManuals(nameof(DbManual.Payee)); // the "previous period" payee may be new
            await ReloadAsync();
        }

        private async Task OnCloseAccount(AccountModel item)
        {
            if (await dialogWrapper.ShowMessageBoxAsync(
                    LocalizationService.Instance.close_account_confirm,
                    LocalizationService.Instance.close_account,
                    yesNoButtons: true))
                await SetActiveAsync(item, false);
        }

        private async Task SetActiveAsync(AccountModel item, bool isActive)
        {
            var account = await db.GetOrCreateAsync<Account>(item.Id ?? 0);
            if (account == null)
                return;

            account.IsActive = isActive;
            await db.InsertOrUpdateAsync(new[] { account });
            await ReloadAsync();
        }

        private static void ApplyDto(Account account, AccountDto dto)
        {
            account.Title = dto.Title;
            account.IsActive = dto.IsActive;
            account.Type = dto.Type;
            account.CurrencyId = dto.CurrencyId;
            account.CardIssuer = dto.CardIssuer;
            account.Issuer = dto.Issuer;
            account.Number = dto.Number;
            account.LimitAmount = dto.LimitAmount;
            account.SortOrder = dto.SortOrder;
            account.IsIncludeIntoTotals = dto.IsIncludeIntoTotals;
            account.Note = dto.Note;
            account.ClosingDay = dto.ClosingDay;
            account.PaymentDay = dto.PaymentDay;
            account.Icon = dto.Icon?.Trim() ?? string.Empty; // NOT NULL columns; Android trims too
            account.AccentColor = dto.AccentColor?.Trim() ?? string.Empty;
        }
    }
}
