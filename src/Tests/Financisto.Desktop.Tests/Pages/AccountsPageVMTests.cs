namespace Financisto.Desktop.Tests.Pages
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Adapter;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Financisto.Desktop.ViewModels.Pages;
    using Financisto.Desktop.Views.Dialogs;
    using Moq;
    using Xunit;

    /// <summary>The accounts page's context menu: Android's account quick actions.</summary>
    public class AccountsPageVMTests : IDisposable
    {
        private readonly Mock<IDialogWrapper> dialogMock = new Mock<IDialogWrapper>();
        private FinancistoDatabase db;
        private AccountsPageVM vm;
        private int cashId;
        private int cardId;

        public void Dispose() => DbManual.ResetAllDatabaseManuals();

        [Fact]
        public async Task Commands_NoAccountSelected_AreDisabled()
        {
            await this.SetupAsync();

            Assert.All(this.AllMenuCommands(), c => Assert.False(c.CanExecute()));
        }

        [Fact]
        public async Task Commands_ActiveAccountSelected_OffersCloseButNotReopen()
        {
            await this.SetupAsync();

            this.Select("Cash");

            Assert.True(this.vm.CloseAccountCommand.CanExecute());
            Assert.False(this.vm.ReopenAccountCommand.CanExecute());
            Assert.True(this.vm.ShowInfoCommand.CanExecute());
            Assert.True(this.vm.UpdateBalanceCommand.CanExecute());
        }

        [Fact]
        public async Task Commands_ClosedAccountSelected_OffersReopenButNotClose()
        {
            await this.SetupAsync();
            await this.SetActive("Cash", false);

            this.Select("Cash");

            Assert.False(this.vm.CloseAccountCommand.CanExecute());
            Assert.True(this.vm.ReopenAccountCommand.CanExecute());
        }

        [Fact]
        public async Task ShowBlotterCommand_ShellHandlesIt_PassesTheAccountId()
        {
            await this.SetupAsync();
            int? shown = null;
            this.vm.ShowAccountTransactions = id =>
            {
                shown = id;
                return Task.CompletedTask;
            };

            this.Select("Card");
            await this.vm.ShowBlotterCommand.ExecuteAsync();

            Assert.Equal(this.cardId, shown);
        }

        [Fact]
        public async Task AddTransactionCommand_OpensTheFormWithTheAccountSelected()
        {
            await this.SetupAsync();
            TransactionDialogVM opened = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<DialogBaseVM>(), 640, 440, LocalizationService.Instance.transaction))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    opened = (TransactionDialogVM)dialog;
                    return Task.FromResult<object>(null);
                });

            this.Select("Card");
            await this.vm.AddTransactionCommand.ExecuteAsync();

            Assert.NotNull(opened);
            Assert.Equal(this.cardId, opened.Transaction.FromAccountId);
            Assert.False(opened.IsUpdateBalanceMode);
        }

        [Fact]
        public async Task AddTransactionCommand_FormSaved_TransactionAddedAndBalanceUpdated()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<DialogBaseVM>(), 640, 440, LocalizationService.Instance.transaction))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    var transaction = ((TransactionDialogVM)dialog).Transaction;
                    transaction.IsAmountNegative = true;
                    transaction.FromAmount = 700;
                    return Task.FromResult<object>(transaction);
                });

            this.Select("Cash");
            await this.vm.AddTransactionCommand.ExecuteAsync();

            Assert.Equal(9300, await this.BalanceOf("Cash"));
            Assert.Equal(9300, this.vm.Entities.Single(x => x.Title == "Cash").TotalAmount);
        }

        [Fact]
        public async Task AddTransferCommand_OpensTheFormWithTheAccountAsSource()
        {
            await this.SetupAsync();
            TransferDialogVM opened = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransferDialog>(It.IsAny<DialogBaseVM>(), 480, 440, LocalizationService.Instance.transfer))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    opened = (TransferDialogVM)dialog;
                    return Task.FromResult<object>(null);
                });

            this.Select("Card");
            await this.vm.AddTransferCommand.ExecuteAsync();

            Assert.Equal(this.cardId, opened.Transfer.FromAccountId);
            Assert.Equal(0, opened.Transfer.FromAmount);
        }

        [Fact]
        public async Task TransferCurrentBalanceCommand_OpensTheTransferWithTheWholeBalance()
        {
            await this.SetupAsync();
            TransferDialogVM opened = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransferDialog>(It.IsAny<DialogBaseVM>(), 480, 440, LocalizationService.Instance.transfer))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    opened = (TransferDialogVM)dialog;
                    return Task.FromResult<object>(null);
                });

            this.Select("Cash");
            await this.vm.TransferCurrentBalanceCommand.ExecuteAsync();

            Assert.Equal(this.cashId, opened.Transfer.FromAccountId);
            Assert.Equal(10000, opened.Transfer.FromAmount);
        }

        [Fact]
        public async Task UpdateBalanceCommand_HigherBalanceEntered_SavesTheDifferenceAsIncome()
        {
            await this.SetupAsync();
            this.SetupBalanceDialog(opened =>
            {
                Assert.True(opened.IsUpdateBalanceMode);
                Assert.Equal(10000, opened.Transaction.RealFromAmount); // starts at the current balance
                Assert.Equal(0, opened.Transaction.BalanceDifference);
                Assert.False(opened.SaveCommand.CanExecute(null)); // nothing to record yet

                opened.Transaction.FromAmount = 12500;

                Assert.Equal(2500, opened.Transaction.BalanceDifference);
                Assert.Contains("25.00", opened.BalanceDifferenceText);
                Assert.Contains("+", opened.BalanceDifferenceText);
                Assert.True(opened.SaveCommand.CanExecute(null));
            });

            this.Select("Cash");
            await this.vm.UpdateBalanceCommand.ExecuteAsync();

            Assert.Equal(12500, await this.BalanceOf("Cash"));
            var newest = (await this.TransactionsOf("Cash")).OrderByDescending(x => x.Id).First();
            Assert.Equal(2500, newest.FromAmount);
        }

        [Fact]
        public async Task UpdateBalanceCommand_LowerBalanceEntered_SavesTheDifferenceAsExpense()
        {
            await this.SetupAsync();
            this.SetupBalanceDialog(opened => opened.Transaction.FromAmount = 9000);

            this.Select("Cash");
            await this.vm.UpdateBalanceCommand.ExecuteAsync();

            Assert.Equal(9000, await this.BalanceOf("Cash"));
            var newest = (await this.TransactionsOf("Cash")).OrderByDescending(x => x.Id).First();
            Assert.Equal(-1000, newest.FromAmount);
        }

        [Fact]
        public async Task UpdateBalanceCommand_AccountInDebt_StartsAtTheNegativeBalance()
        {
            await this.SetupAsync();
            this.SetupBalanceDialog(opened =>
            {
                Assert.True(opened.Transaction.IsAmountNegative);
                Assert.Equal(-2000, opened.Transaction.RealFromAmount);

                opened.Transaction.FromAmount = 500; // still a debt, of 5.00 now
            });

            this.Select("Card");
            await this.vm.UpdateBalanceCommand.ExecuteAsync();

            Assert.Equal(-500, await this.BalanceOf("Card"));
            var newest = (await this.TransactionsOf("Card")).OrderByDescending(x => x.Id).First();
            Assert.Equal(1500, newest.FromAmount);
        }

        [Fact]
        public async Task UpdateBalanceCommand_SwitchedToAnotherAccount_DifferenceIsAgainstThatAccount()
        {
            await this.SetupAsync();
            this.SetupBalanceDialog(opened =>
            {
                opened.Transaction.FromAmount = 10000;
                Assert.Equal(0, opened.Transaction.BalanceDifference);

                // What the account picker does.
                opened.Transaction.FromAccount = DbManual.Account.Single(x => x.Title == "Card");
                opened.Transaction.FromAccountId = this.cardId;

                Assert.Equal(12000, opened.Transaction.BalanceDifference); // 100.00 against the card's -20.00
            });

            this.Select("Cash");
            await this.vm.UpdateBalanceCommand.ExecuteAsync();

            Assert.Equal(10000, await this.BalanceOf("Card"));
            Assert.Equal(10000, await this.BalanceOf("Cash"));
        }

        [Fact]
        public async Task UpdateBalanceCommand_Cancelled_SavesNothing()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<DialogBaseVM>(), 640, 440, LocalizationService.Instance.update_balance))
                .ReturnsAsync((object)null);

            this.Select("Cash");
            await this.vm.UpdateBalanceCommand.ExecuteAsync();

            Assert.Equal(3, (await this.TransactionsOf("Cash")).Count);
            Assert.Equal(10000, await this.BalanceOf("Cash"));
        }

        [Fact]
        public void TransactionDto_UpdateBalance_SplitPartsDivideTheDifference()
        {
            var dto = new TransactionDto();
            dto.StartBalanceUpdate(10000);

            dto.FromAmount = 12500;
            Assert.Equal(2500, dto.UnsplitAmount);

            dto.SubTransactions.Add(new TransactionDto { FromAmount = 1000, IsAmountNegative = false });
            dto.RecalculateUnSplitAmount();
            Assert.Equal(1500, dto.UnsplitAmount);

            dto.SubTransactions.Add(new TransactionDto { FromAmount = 1500, IsAmountNegative = false });
            dto.RecalculateUnSplitAmount();
            Assert.Equal(0, dto.UnsplitAmount);

            dto.ApplyBalanceDifference();
            Assert.False(dto.IsUpdateBalance);
            Assert.Equal(2500, dto.RealFromAmount); // what gets saved is the difference, and the parts still add up to it
            Assert.Equal(0, dto.UnsplitAmount);
        }

        [Fact]
        public void TransactionDto_ApplyBalanceDifference_LowerBalanceBecomesNegative()
        {
            var dto = new TransactionDto();
            dto.StartBalanceUpdate(-500);
            dto.FromAmount = 2000; // sign stays negative: -20.00

            dto.ApplyBalanceDifference();

            Assert.Equal(-1500, dto.RealFromAmount);
            Assert.True(dto.IsAmountNegative);
        }

        [Fact]
        public async Task DeleteOldTransactionsCommand_DateConfirmed_ReplacesOldTransactionsWithTheBalance()
        {
            await this.SetupAsync();
            PurgeAccountDialogVM opened = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<PurgeAccountDialog>(It.IsAny<DialogBaseVM>(), It.IsAny<double>(), It.IsAny<double>(), LocalizationService.Instance.delete_old_transactions))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    opened = (PurgeAccountDialogVM)dialog;
                    return Task.FromResult<object>(new DateTime(2020, 12, 31));
                });
            this.dialogMock
                .Setup(x => x.ShowMessageBoxAsync(It.IsAny<string>(), LocalizationService.Instance.confirm, true))
                .ReturnsAsync(true);

            this.Select("Cash");
            await this.vm.DeleteOldTransactionsCommand.ExecuteAsync();

            Assert.Equal("Cash", opened.AccountTitle);
            Assert.True(opened.Date < DateTime.Now.AddYears(-1)); // Android's default: a year and a day ago

            var transactions = await this.TransactionsOf("Cash");
            Assert.Equal(new long[] { 7000, 3000 }, transactions.OrderBy(x => x.DateTime).Select(x => x.FromAmount));
            Assert.Equal(10000, await this.BalanceOf("Cash"));
            Assert.Contains(DbManual.Payee, x => x.Title == LocalizationService.Instance.purge_account_payee);
        }

        [Fact]
        public async Task DeleteOldTransactionsCommand_NotConfirmed_DeletesNothing()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<PurgeAccountDialog>(It.IsAny<DialogBaseVM>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()))
                .ReturnsAsync(new DateTime(2020, 12, 31));
            this.dialogMock
                .Setup(x => x.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), true))
                .ReturnsAsync(false);

            this.Select("Cash");
            await this.vm.DeleteOldTransactionsCommand.ExecuteAsync();

            Assert.Equal(3, (await this.TransactionsOf("Cash")).Count);
        }

        [Fact]
        public async Task DeleteOldTransactionsCommand_DialogCancelled_AsksNothingMore()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<PurgeAccountDialog>(It.IsAny<DialogBaseVM>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()))
                .ReturnsAsync((object)null);

            this.Select("Cash");
            await this.vm.DeleteOldTransactionsCommand.ExecuteAsync();

            this.dialogMock.Verify(x => x.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            Assert.Equal(3, (await this.TransactionsOf("Cash")).Count);
        }

        [Fact]
        public async Task CloseAccountCommand_Confirmed_AccountBecomesInactive()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowMessageBoxAsync(LocalizationService.Instance.close_account_confirm, It.IsAny<string>(), true))
                .ReturnsAsync(true);

            this.Select("Cash");
            await this.vm.CloseAccountCommand.ExecuteAsync();

            Assert.False(this.vm.Entities.Single(x => x.Title == "Cash").IsActive);
            Assert.Equal(this.cashId, this.vm.Entities.Single(x => !x.IsActive).Id);
            Assert.False(DbManual.Account.Single(x => x.Title == "Cash").IsActive);
        }

        [Fact]
        public async Task CloseAccountCommand_NotConfirmed_AccountStaysActive()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), true))
                .ReturnsAsync(false);

            this.Select("Cash");
            await this.vm.CloseAccountCommand.ExecuteAsync();

            Assert.True(this.vm.Entities.Single(x => x.Title == "Cash").IsActive);
        }

        [Fact]
        public async Task ReopenAccountCommand_AccountBecomesActiveWithoutAsking()
        {
            await this.SetupAsync();
            await this.SetActive("Cash", false);

            this.Select("Cash");
            await this.vm.ReopenAccountCommand.ExecuteAsync();

            Assert.True(this.vm.Entities.Single(x => x.Title == "Cash").IsActive);
            this.dialogMock.Verify(x => x.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task DeleteCommand_Confirmed_AccountAndItsTransactionsAreGone()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowMessageBoxAsync(LocalizationService.Instance.delete_account_confirm, It.IsAny<string>(), true))
                .ReturnsAsync(true);

            this.Select("Cash");
            await this.vm.DeleteCommand.ExecuteAsync();

            Assert.DoesNotContain(this.vm.Entities, x => x.Title == "Cash");
            Assert.DoesNotContain(DbManual.Account, x => x.Title == "Cash");
            using var uow = this.db.CreateUnitOfWork();
            Assert.DoesNotContain(await uow.GetRepository<Transaction>().GetAllAsync(), x => x.FromAccountId == this.cashId);
        }

        [Fact]
        public async Task DeleteCommand_NotConfirmed_NothingIsDeleted()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), true))
                .ReturnsAsync(false);

            this.Select("Cash");
            await this.vm.DeleteCommand.ExecuteAsync();

            Assert.Contains(this.vm.Entities, x => x.Title == "Cash");
            Assert.Equal(3, (await this.TransactionsOf("Cash")).Count);
        }

        [Fact]
        public async Task ShowInfoCommand_EditPressed_OpensTheAccountForEditing()
        {
            await this.SetupAsync();
            AccountInfoDialogVM info = null;
            AccountDialogVM edited = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<AccountInfoDialog>(It.IsAny<DialogBaseVM>(), It.IsAny<double>(), It.IsAny<double>(), LocalizationService.Instance.info))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    info = (AccountInfoDialogVM)dialog;
                    return Task.FromResult(info.OnRequestSave()); // the Edit button
                });
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<AccountDialog>(It.IsAny<DialogBaseVM>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    edited = (AccountDialogVM)dialog;
                    return Task.FromResult<object>(null);
                });

            this.Select("Card");
            await this.vm.ShowInfoCommand.ExecuteAsync();

            Assert.Equal("Card", info.Title);
            Assert.Equal("USD", info.CurrencyTitle);
            Assert.Contains("20.00", info.BalanceText);
            Assert.Contains("-", info.BalanceText);
            Assert.True(info.IsBalanceNegative);
            Assert.Equal("Card", edited.Entity.Title);
        }

        [Fact]
        public async Task ShowInfoCommand_CloseSelected_DoesNotEdit()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<AccountInfoDialog>(It.IsAny<DialogBaseVM>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()))
                .ReturnsAsync((object)null);

            this.Select("Card");
            await this.vm.ShowInfoCommand.ExecuteAsync();

            this.dialogMock.Verify(
                x => x.ShowDialogAsync<AccountDialog>(It.IsAny<DialogBaseVM>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task AccountInfo_CreditCardWithLimit_ShowsAmountAndWhatIsLeftOfTheLimit()
        {
            await this.SetupAsync();
            Account card;
            using (var uow = this.db.CreateUnitOfWork())
            {
                card = await uow.GetRepository<Account>().FindByAsync(x => x.Id == this.cardId, x => x.Currency);
            }

            card.Type = "CREDIT_CARD";
            card.LimitAmount = 100000;
            card.Issuer = "Bank";
            card.Number = "1234";

            var info = new AccountInfoDialogVM(card);

            Assert.True(info.IsCard);
            Assert.Equal("Bank #1234", info.Issuer);
            Assert.True(info.ShowAmount);
            Assert.True(info.IsAmountNegative);
            Assert.False(info.IsBalanceNegative);
            Assert.Contains("980", info.BalanceText); // 1000.00 limit - 20.00 spent
        }

        [Fact]
        public async Task MainWindowVM_ShowBlotterCommand_OpensTheBlotterWithOnlyThatAccount()
        {
            var backupPath = Path.Combine(Environment.CurrentDirectory, "Assets", "min.backup");
            var main = new MainWindowVM(new Mock<IDialogWrapper>().Object, new FinancistoDatabaseFactory(), new EntityReader(), null, null, null);
            await main.OpenBackup(backupPath);
            await main.MenuNavigateCommand.ExecuteAsync(typeof(AccountModel));

            var accounts = Assert.IsType<AccountsPageVM>(main.CurrentPage);
            var account = accounts.Entities.First(x => x.LastTransactionDate > 0);
            accounts.SelectedValue = account;

            await accounts.ShowBlotterCommand.ExecuteAsync();

            // The blotter page is the one from the backup opening, with every transaction until the filter is applied.
            var blotter = await WaitFor(() =>
                main.CurrentPage is BlotterPageVM page
                && page.Entities.Count > 0
                && page.Entities.All(x => x.FromAccountId == account.Id || x.ToAccountId == account.Id) ? page : null);

            Assert.Equal(account.Id, Assert.Single(blotter.SelectedAccounts).Id);
            Assert.All(blotter.Entities, x => Assert.True(x.FromAccountId == account.Id || x.ToAccountId == account.Id));
            Assert.Equal(typeof(BlotterModel), main.SelectedItemTop.ModelType);
        }

        private static async Task<T> WaitFor<T>(Func<T> probe)
            where T : class
        {
            for (var i = 0; i < 100; i++)
            {
                var result = probe();
                if (result != null)
                {
                    return result;
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("The page did not show up.");
        }

        /// <summary>Two accounts: Cash with 100.00 (three transactions, two of them from before 2021) and Card with -20.00.</summary>
        private async Task SetupAsync()
        {
            this.db = new FinancistoDatabase();
            await this.db.SeedAsync();

            using (var uow = this.db.CreateUnitOfWork())
            {
                var currency = new Currency { Id = 0, Title = "USD", IsDefault = true, IsActive = true, Name = "USD", Decimals = 2, Symbol = "$", SymbolFormat = "RS", NumberFormat = "#,##0.00" };
                await uow.GetRepository<Currency>().AddAsync(currency);
                await uow.SaveChangesAsync();

                await uow.GetRepository<Account>().AddAsync(new Account { Id = 0, Title = "Cash", CurrencyId = currency.Id, Type = "CASH", IsActive = true, SortOrder = 1 });
                await uow.GetRepository<Account>().AddAsync(new Account { Id = 0, Title = "Card", CurrencyId = currency.Id, Type = "DEBIT_CARD", CardIssuer = "VISA", IsActive = true, SortOrder = 2 });
                await uow.SaveChangesAsync();
            }

            using (var uow = this.db.CreateUnitOfWork())
            {
                var accounts = await uow.GetRepository<Account>().GetAllAsync();
                this.cashId = accounts.Single(x => x.Title == "Cash").Id;
                this.cardId = accounts.Single(x => x.Title == "Card").Id;
            }

            await this.db.AddTransactionsAsync(new[]
            {
                Tx(this.cashId, 10000, new DateTime(2020, 1, 10, 12, 0, 0)),
                Tx(this.cashId, -3000, new DateTime(2020, 2, 10, 12, 0, 0)),
                Tx(this.cashId, 3000, new DateTime(2021, 6, 1, 12, 0, 0)),
                Tx(this.cardId, -2000, new DateTime(2020, 3, 1, 12, 0, 0)),
            });
            await this.db.RebuildAccountBalanceAsync(this.cashId);
            await this.db.RebuildAccountBalanceAsync(this.cardId);

            DbManual.ResetAllDatabaseManuals();
            await DbManual.SetupAsync(this.db);

            this.vm = new AccountsPageVM(this.db, this.dialogMock.Object);
            await this.vm.RefreshDataCommand.ExecuteAsync();
        }

        private static Transaction Tx(int accountId, long amount, DateTime local) => new Transaction
        {
            Id = 0,
            FromAccountId = accountId,
            FromAmount = amount,
            DateTime = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local)).ToUnixTimeMilliseconds(),
            OriginalCurrencyId = 0,
            OriginalFromAmount = 0,
        };

        private void Select(string title) => this.vm.SelectedValue = this.vm.Entities.Single(x => x.Title == title);

        private System.Collections.Generic.IEnumerable<Financisto.Common.IAsyncCommand> AllMenuCommands() => new[]
        {
            this.vm.ShowInfoCommand,
            this.vm.ShowBlotterCommand,
            this.vm.EditCommand,
            this.vm.AddTransactionCommand,
            this.vm.AddTransferCommand,
            this.vm.UpdateBalanceCommand,
            this.vm.DeleteOldTransactionsCommand,
            this.vm.CloseAccountCommand,
            this.vm.ReopenAccountCommand,
            this.vm.DeleteCommand,
            this.vm.TransferCurrentBalanceCommand,
        };

        private async Task SetActive(string title, bool isActive)
        {
            using (var uow = this.db.CreateUnitOfWork())
            {
                var account = (await uow.GetRepository<Account>().GetAllAsync()).Single(x => x.Title == title);
                account.IsActive = isActive;
                await this.db.InsertOrUpdateAsync(new[] { account });
            }

            DbManual.ResetManuals(nameof(DbManual.Account));
            await DbManual.SetupAsync(this.db);
            await this.vm.RefreshDataCommand.ExecuteAsync();
        }

        /// <summary>The balance dialog: <paramref name="enterBalance"/> plays the user, then the form is saved.</summary>
        private void SetupBalanceDialog(Action<TransactionDialogVM> enterBalance)
        {
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<DialogBaseVM>(), 640, 440, LocalizationService.Instance.update_balance))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    var opened = (TransactionDialogVM)dialog;
                    enterBalance(opened);
                    return Task.FromResult<object>(opened.Transaction);
                });
        }

        private async Task<long> BalanceOf(string title)
        {
            using var uow = this.db.CreateUnitOfWork();
            return (await uow.GetRepository<Account>().GetAllAsync()).Single(x => x.Title == title).TotalAmount;
        }

        private async Task<System.Collections.Generic.List<Transaction>> TransactionsOf(string title)
        {
            var id = title == "Cash" ? this.cashId : this.cardId;
            using var uow = this.db.CreateUnitOfWork();
            return await uow.GetRepository<Transaction>().FindManyAsync(x => x.FromAccountId == id);
        }
    }
}
