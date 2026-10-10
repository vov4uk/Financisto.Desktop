namespace Financisto.Desktop.Tests.Pages
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Financisto.Desktop.ViewModels.Pages;
    using Financisto.Desktop.Views.Dialogs;
    using Moq;
    using Xunit;

    /// <summary>
    /// The blotter filtered by account(s) behaves like Android's account blotter (v_blotter_for_account_with_splits): a split part that
    /// moved money into the account is a row of its own, and a transaction is never listed twice.
    /// </summary>
    public class BlotterAccountFilterTests : IDisposable
    {
        private readonly Mock<IDialogWrapper> dialogMock = new Mock<IDialogWrapper>();
        private FinancistoDatabase db;
        private BlotterPageVM vm;
        private int monoId;
        private int revolutId;
        private int visaId;
        private int splitId;
        private int commissionId;
        private int splitTransferId;
        private int plainTransferId;
        private int revolutOwnId;
        private int revolutToVisaId;

        public void Dispose() => DbManual.ResetAllDatabaseManuals();

        [Fact]
        public async Task NoAccountFiltered_EveryTransactionOnce_SplitPartsAreInsideTheirParent()
        {
            await this.SetupAsync();

            var rows = await this.RowsFor();

            Assert.Equal(
                new[] { this.splitId, this.plainTransferId, this.revolutOwnId, this.revolutToVisaId }.OrderBy(x => x),
                rows.Select(x => x.Id).OrderBy(x => x));
            Assert.All(rows, x => Assert.False(x.IsAccountPerspective));
        }

        [Fact]
        public async Task SingleAccount_TheSplitParentsAccount_ShowsTheParentAndNotItsParts()
        {
            await this.SetupAsync();

            var rows = await this.RowsFor("Mono");

            Assert.Contains(rows, x => x.Id == this.splitId);
            Assert.DoesNotContain(rows, x => x.Id == this.commissionId || x.Id == this.splitTransferId);
            Assert.Single(rows, x => x.Id == this.splitId);
            Assert.All(rows, x => Assert.Equal(this.monoId, x.FromAccountId));
        }

        [Fact]
        public async Task SingleAccount_ReceivingAPartOfAnotherAccountsSplit_ShowsThePartAsATransferIn()
        {
            await this.SetupAsync();

            var rows = await this.RowsFor("Revolut");

            var part = Assert.Single(rows, x => x.Id == this.splitTransferId);
            Assert.True(part.IsSplitPart);
            Assert.Equal(this.splitId, part.ParentId);
            Assert.True(part.IsIncomingTransfer);
            Assert.Equal("Transfer", part.Type);
            Assert.Equal(this.revolutId, part.FromAccountId); // seen from Revolut
            Assert.Equal(this.monoId, part.ToAccountId);
            Assert.Equal(100000, part.FromAmount);
            Assert.Equal("Mono » Revolut", part.AccountTitle); // the direction the money went
            Assert.Contains("+", part.AmountTitle);
            Assert.Equal("100000", new string(part.AmountTitle.Where(char.IsDigit).ToArray())); // 1000.00, whatever the separators

            // neither the parent nor the commission are Revolut's
            Assert.DoesNotContain(rows, x => x.Id == this.splitId || x.Id == this.commissionId);
        }

        [Fact]
        public async Task SingleAccount_ABlotterOfOneAccount_ListsEachRowWithThatAccountsBalance()
        {
            await this.SetupAsync();

            var rows = await this.RowsFor("Revolut");

            Assert.All(rows, x => Assert.True(x.IsAccountPerspective));
            Assert.All(rows, x => Assert.Equal(this.revolutId, x.FromAccountId));

            // the newest row carries the account's whole balance: 1000.00 + 500.00 + 20.00 - 30.00
            var newest = rows.OrderByDescending(x => x.Datetime).ThenByDescending(x => x.Id).First();
            Assert.Equal(149000, newest.FromAccountBalance);
            Assert.Equal(newest.BalanceTitle, BalanceText(newest.FromAccountCurrency, 149000));
        }

        [Fact]
        public async Task MultipleAccounts_BothSidesOfATransfer_ListedOnce()
        {
            await this.SetupAsync();

            var rows = await this.RowsFor("Mono", "Revolut");

            Assert.Equal(rows.Count, rows.Select(x => x.Id).Distinct().Count());
            var transfer = Assert.Single(rows, x => x.Id == this.plainTransferId);
            Assert.False(transfer.IsIncomingTransfer); // the side of the account it left
            Assert.Equal(this.monoId, transfer.FromAccountId);
        }

        [Fact]
        public async Task MultipleAccounts_ParentAndItsPartForTheOtherSelectedAccount_ParentOnly()
        {
            await this.SetupAsync();

            var rows = await this.RowsFor("Mono", "Revolut");

            Assert.Single(rows, x => x.Id == this.splitId);
            Assert.DoesNotContain(rows, x => x.Id == this.splitTransferId || x.Id == this.commissionId);
        }

        [Fact]
        public async Task MultipleAccounts_ParentsAccountNotSelected_ThePartsStillShow()
        {
            await this.SetupAsync();

            var rows = await this.RowsFor("Revolut", "Visa");

            Assert.Equal(rows.Count, rows.Select(x => x.Id).Distinct().Count());
            Assert.Contains(rows, x => x.Id == this.splitTransferId && x.IsSplitPart);
            Assert.DoesNotContain(rows, x => x.Id == this.splitId);

            // Revolut -> Visa is between two selected accounts: once, from Revolut
            var transfer = Assert.Single(rows, x => x.Id == this.revolutToVisaId);
            Assert.Equal(this.revolutId, transfer.FromAccountId);
        }

        [Fact]
        public async Task EditCommand_ASplitPart_OpensTheParentSplit()
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
            await this.RowsFor("Revolut");

            this.vm.SelectedValue = this.vm.Entities.Single(x => x.Id == this.splitTransferId);
            await this.vm.EditCommand.ExecuteAsync();

            Assert.NotNull(opened);
            Assert.Equal(this.splitId, opened.Transaction.Id);
            Assert.True(opened.Transaction.IsSplitCategory);
            Assert.Equal(2, opened.Transaction.SubTransactions.Count);
        }

        [Fact]
        public async Task DuplicateCommand_ASplitPart_DuplicatesTheParentSplit()
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
            await this.RowsFor("Revolut");

            this.vm.SelectedValue = this.vm.Entities.Single(x => x.Id == this.splitTransferId);
            await this.vm.DuplicateCommand.ExecuteAsync();

            Assert.Equal(0, opened.Transaction.Id); // a copy
            Assert.Equal(2, opened.Transaction.SubTransactions.Count);
            Assert.Equal(this.monoId, opened.Transaction.FromAccountId);
        }

        [Fact]
        public async Task DeleteCommand_ASplitPart_AsksAndDeletesTheWholeSplit()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowMessageBoxAsync(LocalizationService.Instance.delete_transaction_parent_confirm, It.IsAny<string>(), true))
                .ReturnsAsync(true);
            await this.RowsFor("Revolut");

            this.vm.SelectedValue = this.vm.Entities.Single(x => x.Id == this.splitTransferId);
            await this.vm.DeleteCommand.ExecuteAsync();

            using var uow = this.db.CreateUnitOfWork();
            var left = await uow.GetRepository<Transaction>().GetAllAsync();
            Assert.DoesNotContain(left, x => x.Id == this.splitId || x.Id == this.commissionId || x.Id == this.splitTransferId);
            Assert.Equal(3, left.Count);

            // Revolut loses the 1000.00 it received, Mono the whole split
            var accounts = await uow.GetRepository<Account>().GetAllAsync();
            Assert.Equal(49000, accounts.Single(x => x.Id == this.revolutId).TotalAmount);
            Assert.Equal(-50000, accounts.Single(x => x.Id == this.monoId).TotalAmount);
        }

        [Fact]
        public async Task DeleteCommand_ASplitPart_NotConfirmed_DeletesNothing()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), true))
                .ReturnsAsync(false);
            await this.RowsFor("Revolut");

            this.vm.SelectedValue = this.vm.Entities.Single(x => x.Id == this.splitTransferId);
            await this.vm.DeleteCommand.ExecuteAsync();

            using var uow = this.db.CreateUnitOfWork();
            Assert.Equal(6, (await uow.GetRepository<Transaction>().GetAllAsync()).Count);
        }

        private static string BalanceText(CurrencyModel currency, long amount) => Financisto.Common.Utils.BlotterUtils.SetAmountText(currency, amount, false);

        /// <summary>
        /// Mono: split of -1010.00 (a -10.00 commission and a -1000.00 transfer to Revolut), and a -500.00 transfer to Revolut.
        /// Revolut: also +20.00 of its own and a -30.00 transfer to Visa.
        /// </summary>
        private async Task SetupAsync()
        {
            this.db = new FinancistoDatabase();
            await this.db.SeedAsync();

            using (var uow = this.db.CreateUnitOfWork())
            {
                var usd = new Currency { Id = 0, Title = "USD", IsDefault = true, IsActive = true, Name = "USD", Decimals = 2, Symbol = "$", SymbolFormat = "RS", NumberFormat = "#,##0.00" };
                await uow.GetRepository<Currency>().AddAsync(usd);
                await uow.SaveChangesAsync();

                foreach (var (title, order) in new[] { ("Mono", 1), ("Revolut", 2), ("Visa", 3) })
                {
                    await uow.GetRepository<Account>().AddAsync(new Account { Id = 0, Title = title, CurrencyId = usd.Id, Type = "CASH", IsActive = true, SortOrder = order });
                }

                await uow.SaveChangesAsync();
            }

            using (var uow = this.db.CreateUnitOfWork())
            {
                var accounts = await uow.GetRepository<Account>().GetAllAsync();
                this.monoId = accounts.Single(x => x.Title == "Mono").Id;
                this.revolutId = accounts.Single(x => x.Title == "Revolut").Id;
                this.visaId = accounts.Single(x => x.Title == "Visa").Id;
            }

            var split = Tx(this.monoId, -101000, 1, category: -1);
            await this.db.AddTransactionsAsync(new[] { split });
            this.splitId = split.Id;

            var commission = Tx(this.monoId, -1000, 1);
            commission.ParentId = split.Id;
            commission.ParentAccountId = this.monoId;
            var splitTransfer = Tx(this.monoId, -100000, 1, to: this.revolutId, toAmount: 100000);
            splitTransfer.ParentId = split.Id;
            splitTransfer.ParentAccountId = this.monoId;
            var plainTransfer = Tx(this.monoId, -50000, 2, to: this.revolutId, toAmount: 50000);
            var revolutOwn = Tx(this.revolutId, 2000, 3);
            var revolutToVisa = Tx(this.revolutId, -3000, 4, to: this.visaId, toAmount: 3000);
            await this.db.AddTransactionsAsync(new[] { commission, splitTransfer, plainTransfer, revolutOwn, revolutToVisa });
            this.commissionId = commission.Id;
            this.splitTransferId = splitTransfer.Id;
            this.plainTransferId = plainTransfer.Id;
            this.revolutOwnId = revolutOwn.Id;
            this.revolutToVisaId = revolutToVisa.Id;

            foreach (var id in new[] { this.monoId, this.revolutId, this.visaId })
            {
                await this.db.RebuildAccountBalanceAsync(id);
            }

            DbManual.ResetAllDatabaseManuals();
            await DbManual.SetupAsync(this.db);

            this.dialogMock.Setup(x => x.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(false);
            this.vm = new BlotterPageVM(this.db, this.dialogMock.Object);
        }

        private static Transaction Tx(int accountId, long amount, int day, int category = 0, int to = 0, long toAmount = 0) => new Transaction
        {
            Id = 0,
            FromAccountId = accountId,
            FromAmount = amount,
            ToAccountId = to,
            ToAmount = toAmount,
            CategoryId = category,
            DateTime = new DateTimeOffset(new DateTime(2020, 9, day, 14, 20, 0, DateTimeKind.Local)).ToUnixTimeMilliseconds(),
            OriginalCurrencyId = 0,
            OriginalFromAmount = 0,
        };

        private async Task<ObservableCollection<BlotterModel>> RowsFor(params string[] accountTitles)
        {
            this.vm.SelectedAccounts = new ObservableCollection<AccountFilterModel>(
                accountTitles.Select(t => DbManual.Account.Single(a => a.Title == t)));
            await this.vm.RefreshDataCommand.ExecuteAsync();
            return this.vm.Entities;
        }
    }
}
