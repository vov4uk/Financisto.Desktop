namespace Financisto.DataAccess.Tests
{
    using System;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Data;
    using Financisto.DataAccess.View;
    using Xunit;

    /// <summary>Templates (<c>is_template = 1</c>, Android's) are in <c>v_all_transactions</c> only: no blotter, balance or report counts them.</summary>
    public class TemplateViewsTest
    {
        [Fact]
        public async Task Templates_AreOnlyInTheAllTransactionsView()
        {
            var (db, accountId) = await Setup();
            var template = Tx(accountId, -700, 1, isTemplate: 1);
            var ordinary = Tx(accountId, -300, 2);
            await db.AddTransactionsAsync(new[] { template, ordinary });
            var part = Tx(accountId, -100, 1, isTemplate: 1);
            part.ParentId = template.Id;
            part.ParentAccountId = accountId;
            await db.AddTransactionsAsync(new[] { part });

            using var uow = db.CreateUnitOfWork();

            var all = await uow.GetRepository<AllTransactions>().GetAllAsync();
            Assert.Equal(3, all.Count);
            Assert.Equal(new[] { template.Id }, all.Where(x => x.IsTemplate == 1 && x.ParentId == 0).Select(x => x.Id));

            Assert.Equal(new[] { ordinary.Id }, (await uow.GetRepository<BlotterTransactions>().GetAllAsync()).Select(x => x.Id));
            Assert.Equal(new[] { ordinary.Id }, (await uow.GetRepository<BlotterTransactionsForAccountWithSplits>().GetAllAsync()).Select(x => x.Id));
        }

        [Fact]
        public async Task Templates_DoNotCountInBalancesOrReports()
        {
            var (db, accountId) = await Setup();
            await db.AddTransactionsAsync(new[] { Tx(accountId, -700, 1, isTemplate: 1), Tx(accountId, -300, 2) });

            await db.RebuildAccountBalanceAsync(accountId);

            using (var uow = db.CreateUnitOfWork())
            {
                Assert.Equal(-300, (await uow.GetRepository<Account>().GetAllAsync()).Single().TotalAmount);
            }

            var reported = await db.ExecuteQuery<ReportRow>("SELECT from_amount FROM v_report_transactions");
            Assert.Equal(new long[] { -300 }, reported.Select(x => x.FromAmount));
        }

        private static async Task<(FinancistoDatabase db, int accountId)> Setup()
        {
            var db = new FinancistoDatabase();
            await db.SeedAsync();

            using var uow = db.CreateUnitOfWork();
            var currency = new Currency { Id = 0, Title = "Dollar", IsDefault = true, IsActive = true, Name = "USD", Decimals = 2, Symbol = "$", SymbolFormat = "." };
            await uow.GetRepository<Currency>().AddAsync(currency);
            await uow.SaveChangesAsync();

            var account = new Account { Id = 0, Title = "A", CurrencyId = currency.Id, Type = "CASH", IsActive = true };
            await uow.GetRepository<Account>().AddAsync(account);
            await uow.SaveChangesAsync();
            return (db, account.Id);
        }

        private static Transaction Tx(int accountId, long amount, int day, int isTemplate = 0) => new Transaction
        {
            Id = 0,
            FromAccountId = accountId,
            FromAmount = amount,
            DateTime = new DateTimeOffset(new DateTime(2020, 5, day, 12, 0, 0, DateTimeKind.Local)).ToUnixTimeMilliseconds(),
            OriginalCurrencyId = 0,
            OriginalFromAmount = 0,
            IsTemplate = isTemplate,
        };

        private class ReportRow
        {
            [Column("from_amount")]
            public long FromAmount { get; set; }
        }
    }
}
