namespace Financisto.Desktop.Tests.Pages
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Helpers;
    using Xunit;

    /// <summary>Templates are <c>is_template = 1</c> transactions (Android): reading them, turning one into a transaction, saving one from a transaction.</summary>
    public class TemplateStoreTests : TemplatesTestBase
    {
        [Fact]
        public async Task GetTemplates_ListsOnlyTheTemplates_NewestFirst()
        {
            await this.SetupAsync();

            var templates = await new TemplateStore(this.db).GetTemplatesAsync();

            // not the ordinary transaction and not the parts of the split template
            Assert.Equal(new[] { this.groceriesId, this.toBankId, this.coffeeId }, templates.Select(x => x.Id));
        }

        [Fact]
        public async Task GetTemplates_RowsCarryTheNameAndWhatTheListShows()
        {
            await this.SetupAsync();

            var templates = await new TemplateStore(this.db).GetTemplatesAsync();

            var coffee = templates.Single(x => x.Id == this.coffeeId);
            Assert.Equal("Coffee", coffee.TemplateName);
            Assert.Equal("Cash", coffee.AccountTitle);
            Assert.Equal("Food", coffee.CategoryTitle);
            Assert.Equal("Expense", coffee.Type);
            Assert.Equal(-350, coffee.FromAmount);

            var toBank = templates.Single(x => x.Id == this.toBankId);
            Assert.Equal("Transfer", toBank.Type);
            Assert.Equal("To bank", toBank.TemplateName);

            Assert.Equal("Share", templates.Single(x => x.Id == this.groceriesId).Type);
        }

        [Fact]
        public async Task Templates_DoNotMoveAnyMoney()
        {
            await this.SetupAsync();

            // only the ordinary -5.00 counts: the templates are in neither the balances nor the blotter views
            Assert.Equal(-500, (await this.AccountAsync(this.cashId)).TotalAmount);
            Assert.Equal(0, (await this.AccountAsync(this.bankId)).TotalAmount);
        }

        [Fact]
        public async Task CreateFromTemplate_IsANewTransactionDatedNow_TheTemplateStaysUntouched()
        {
            await this.SetupAsync();
            var before = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            var created = await new TemplateStore(this.db).CreateFromTemplateAsync(this.coffeeId, 1);

            var (transaction, parts) = created.Value;
            Assert.Equal(0, transaction.Id);
            Assert.Equal(0, transaction.IsTemplate);
            Assert.Null(transaction.TemplateName);
            Assert.InRange(transaction.DateTime, before, DateTimeOffset.Now.ToUnixTimeMilliseconds());
            Assert.Equal(this.cashId, transaction.FromAccountId);
            Assert.Equal(this.foodId, transaction.CategoryId);
            Assert.Equal(-350, transaction.FromAmount);
            Assert.Empty(parts);

            var template = (await this.AllTransactionsAsync()).Single(x => x.Id == this.coffeeId);
            Assert.Equal(1, template.IsTemplate);
            Assert.Equal("Coffee", template.TemplateName);
        }

        [Fact]
        public async Task CreateFromTemplate_MultiplierScalesTheAmounts()
        {
            await this.SetupAsync();
            var store = new TemplateStore(this.db);

            var coffee = (await store.CreateFromTemplateAsync(this.coffeeId, 3)).Value.Transaction;
            var transfer = (await store.CreateFromTemplateAsync(this.toBankId, 2)).Value.Transaction;

            Assert.Equal(-1050, coffee.FromAmount);
            Assert.Equal(-20000, transfer.FromAmount);
            Assert.Equal(20000, transfer.ToAmount);
            Assert.True(TemplateStore.IsTransfer(transfer));
            Assert.False(TemplateStore.IsTransfer(coffee));
        }

        [Fact]
        public async Task CreateFromTemplate_AForeignAmountScalesWithIt_SoTheRateStays()
        {
            await this.SetupAsync();
            using (var uow = this.db.CreateUnitOfWork())
            {
                var coffee = (await uow.GetRepository<Transaction>().GetAllAsync()).Single(x => x.Id == this.coffeeId);
                coffee.OriginalCurrencyId = 5;
                coffee.OriginalFromAmount = -100;
                await uow.GetRepository<Transaction>().UpdateAsync(coffee);
                await uow.SaveChangesAsync();
            }

            var created = (await new TemplateStore(this.db).CreateFromTemplateAsync(this.coffeeId, 4)).Value.Transaction;

            Assert.Equal(-1400, created.FromAmount);
            Assert.Equal(-400, created.OriginalFromAmount);
        }

        [Fact]
        public async Task CreateFromTemplate_ASplit_BringsItsPartsAsNewTransactions()
        {
            await this.SetupAsync();

            var (transaction, parts) = (await new TemplateStore(this.db).CreateFromTemplateAsync(this.groceriesId, 2)).Value;

            Assert.Equal(0, transaction.Id);
            Assert.Equal(-6000, transaction.FromAmount);
            Assert.Equal(2, parts.Count);
            Assert.All(parts, x => Assert.Equal(0, x.Id));
            Assert.All(parts, x => Assert.Equal(0, x.IsTemplate));
            Assert.Equal(new long[] { -4000, -2000 }, parts.Select(x => x.FromAmount).OrderBy(x => x));
        }

        [Fact]
        public async Task CreateFromTemplate_UnknownTemplate_ReturnsNull()
        {
            await this.SetupAsync();
            var store = new TemplateStore(this.db);

            Assert.Null(await store.CreateFromTemplateAsync(9999, 1));
            Assert.Null(await store.CreateFromTemplateAsync(0, 1));
        }

        [Fact]
        public async Task SaveAsTemplate_CopiesTheTransactionAsATemplate_WithoutChangingBalances()
        {
            await this.SetupAsync();

            var id = await new TemplateStore(this.db).SaveAsTemplateAsync(this.ordinaryId);

            var all = await this.AllTransactionsAsync();
            var copy = all.Single(x => x.Id == id);
            Assert.NotEqual(this.ordinaryId, id);
            Assert.Equal(1, copy.IsTemplate);
            Assert.Equal(-500, copy.FromAmount);
            Assert.Equal(this.foodId, copy.CategoryId);
            Assert.True(string.IsNullOrEmpty(copy.TemplateName)); // Android leaves it for the user to name

            Assert.Equal(0, all.Single(x => x.Id == this.ordinaryId).IsTemplate);
            await this.db.RebuildAccountBalanceAsync(this.cashId);
            Assert.Equal(-500, (await this.AccountAsync(this.cashId)).TotalAmount);
        }

        [Fact]
        public async Task SaveAsTemplate_ASplit_CopiesItsPartsUnderTheNewParent()
        {
            await this.SetupAsync();

            // a part of a split stands for its parent, like Android's BlotterOperations
            var id = await new TemplateStore(this.db).SaveAsTemplateAsync(this.groceriesFoodPartId);

            var all = await this.AllTransactionsAsync();
            var parent = all.Single(x => x.Id == id);
            var parts = all.Where(x => x.ParentId == id).ToList();
            Assert.Equal(-1, parent.CategoryId);
            Assert.Equal(1, parent.IsTemplate);
            Assert.Equal(2, parts.Count);
            Assert.All(parts, x => Assert.Equal(1, x.IsTemplate));
            Assert.Equal(new long[] { -2000, -1000 }, parts.Select(x => x.FromAmount).OrderBy(x => x));

            // the original split still has its own parts
            Assert.Equal(2, all.Count(x => x.ParentId == this.groceriesId));
        }

        [Fact]
        public async Task DeleteTemplate_RemovesTheTemplateAndItsParts_NothingElse()
        {
            await this.SetupAsync();
            var before = (await this.AllTransactionsAsync()).Count;

            await new TemplateStore(this.db).DeleteTemplateAsync(this.groceriesId);

            var left = await this.AllTransactionsAsync();
            Assert.Equal(before - 3, left.Count);
            Assert.DoesNotContain(left, x => x.Id == this.groceriesId || x.ParentId == this.groceriesId);
            Assert.Contains(left, x => x.Id == this.coffeeId);
            Assert.Contains(left, x => x.Id == this.ordinaryId);
        }

        [Fact]
        public async Task DeleteTemplate_AnOrdinaryTransaction_IsNotDeleted()
        {
            await this.SetupAsync();
            var before = (await this.AllTransactionsAsync()).Count;

            await new TemplateStore(this.db).DeleteTemplateAsync(this.ordinaryId);
            await new TemplateStore(this.db).DeleteTemplateAsync(0);

            Assert.Equal(before, (await this.AllTransactionsAsync()).Count);
        }
    }
}
