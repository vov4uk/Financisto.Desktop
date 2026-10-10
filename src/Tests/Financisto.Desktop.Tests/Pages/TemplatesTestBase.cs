namespace Financisto.Desktop.Tests.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Helpers;
    using Moq;

    /// <summary>
    /// A small database with Android-style templates (<c>is_template = 1</c>): "Coffee" (Cash, Food, -3.50), "To bank" (a transfer Cash to Bank),
    /// "Groceries" (a split of -30.00 with two parts) and, next to them, one ordinary -5.00 transaction.
    /// </summary>
    public abstract class TemplatesTestBase : IDisposable
    {
        protected readonly Mock<IDialogWrapper> dialogMock = new Mock<IDialogWrapper>();
        protected FinancistoDatabase db;
        protected int cashId;
        protected int bankId;
        protected int foodId;
        protected int coffeeId;
        protected int toBankId;
        protected int groceriesId;
        protected int groceriesFoodPartId;
        protected int groceriesOtherPartId;
        protected int ordinaryId;

        public void Dispose() => DbManual.ResetAllDatabaseManuals();

        protected async Task SetupAsync()
        {
            this.db = new FinancistoDatabase();
            await this.db.SeedAsync();

            using (var uow = this.db.CreateUnitOfWork())
            {
                var usd = new Currency { Id = 0, Title = "USD", IsDefault = true, IsActive = true, Name = "USD", Decimals = 2, Symbol = "$", SymbolFormat = "RS", NumberFormat = "#,##0.00" };
                await uow.GetRepository<Currency>().AddAsync(usd);
                await uow.SaveChangesAsync();

                foreach (var (title, order) in new[] { ("Cash", 1), ("Bank", 2) })
                {
                    await uow.GetRepository<Account>().AddAsync(new Account { Id = 0, Title = title, CurrencyId = usd.Id, Type = "CASH", IsActive = true, SortOrder = order });
                }

                await uow.GetRepository<Category>().AddAsync(new Category { Id = 0, Title = "Food", Left = 3, Right = 4, Type = 0 });
                await uow.SaveChangesAsync();
            }

            using (var uow = this.db.CreateUnitOfWork())
            {
                var accounts = await uow.GetRepository<Account>().GetAllAsync();
                this.cashId = accounts.Single(x => x.Title == "Cash").Id;
                this.bankId = accounts.Single(x => x.Title == "Bank").Id;
                this.foodId = (await uow.GetRepository<Category>().GetAllAsync()).Single(x => x.Title == "Food").Id;
            }

            var coffee = Tx(this.cashId, -350, 1, category: this.foodId, template: "Coffee");
            var toBank = Tx(this.cashId, -10000, 2, to: this.bankId, toAmount: 10000, template: "To bank");
            var groceries = Tx(this.cashId, -3000, 3, category: -1, template: "Groceries");
            var ordinary = Tx(this.cashId, -500, 4, category: this.foodId);
            await this.db.AddTransactionsAsync(new[] { coffee, toBank, groceries, ordinary });

            var foodPart = Tx(this.cashId, -2000, 3, category: this.foodId, isTemplate: true);
            foodPart.ParentId = groceries.Id;
            foodPart.ParentAccountId = this.cashId;
            var otherPart = Tx(this.cashId, -1000, 3, isTemplate: true);
            otherPart.ParentId = groceries.Id;
            otherPart.ParentAccountId = this.cashId;
            await this.db.AddTransactionsAsync(new[] { foodPart, otherPart });

            this.coffeeId = coffee.Id;
            this.toBankId = toBank.Id;
            this.groceriesId = groceries.Id;
            this.groceriesFoodPartId = foodPart.Id;
            this.groceriesOtherPartId = otherPart.Id;
            this.ordinaryId = ordinary.Id;

            foreach (var id in new[] { this.cashId, this.bankId })
            {
                await this.db.RebuildAccountBalanceAsync(id);
            }

            DbManual.ResetAllDatabaseManuals();
            await DbManual.SetupAsync(this.db);

            this.dialogMock.Setup(x => x.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(false);
        }

        /// <summary>A template row when it has a <paramref name="template"/> name (a part of a template has none: <paramref name="isTemplate"/>), else a plain one.</summary>
        protected Transaction Tx(int accountId, long amount, int day, int category = 0, int to = 0, long toAmount = 0, string template = null, bool isTemplate = false) => new Transaction
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
            IsTemplate = template != null || isTemplate ? 1 : 0,
            TemplateName = template,
        };

        protected async Task<List<Transaction>> AllTransactionsAsync()
        {
            using var uow = this.db.CreateUnitOfWork();
            return await uow.GetRepository<Transaction>().GetAllAsync();
        }

        protected async Task<Account> AccountAsync(int id)
        {
            using var uow = this.db.CreateUnitOfWork();
            return (await uow.GetRepository<Account>().GetAllAsync()).Single(x => x.Id == id);
        }
    }
}
