namespace Financisto.DataAccess.Tests
{
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Data;
    using Financisto.DataAccess.View;
    using Xunit;

    /// <summary>Amounts are cents in 64 bits everywhere; the blotter views' balance columns used to be read as 32-bit.</summary>
    public class BlotterBalanceRangeTest
    {
        // 30,000,000.00 in cents, more than int.MaxValue (2,147,483,647).
        private const long Big = 3_000_000_000;

        [Fact]
        public async Task BlotterViews_BalanceBeyond32Bit_AreReadWithoutOverflow()
        {
            var db = new FinancistoDatabase();
            await db.SeedAsync();

            int a;
            int b;
            using (var uow = db.CreateUnitOfWork())
            {
                var currency = new Currency { Id = 0, Title = "UAH", IsDefault = true, IsActive = true, Name = "UAH", Decimals = 2, Symbol = "u", SymbolFormat = "RS" };
                await uow.GetRepository<Currency>().AddAsync(currency);
                await uow.SaveChangesAsync();
                await uow.GetRepository<Account>().AddAsync(new Account { Id = 0, Title = "A", CurrencyId = currency.Id, Type = "CASH", IsActive = true });
                await uow.GetRepository<Account>().AddAsync(new Account { Id = 0, Title = "B", CurrencyId = currency.Id, Type = "CASH", IsActive = true });
                await uow.SaveChangesAsync();
                var accounts = (await uow.GetRepository<Account>().GetAllAsync()).OrderBy(x => x.Title).ToList();
                a = accounts[0].Id;
                b = accounts[1].Id;
            }

            await db.AddTransactionsAsync(new[]
            {
                new Transaction { Id = 0, FromAccountId = a, FromAmount = Big, DateTime = 1, OriginalCurrencyId = 0, OriginalFromAmount = 0 },
                new Transaction { Id = 0, FromAccountId = a, FromAmount = -Big, ToAccountId = b, ToAmount = Big, DateTime = 2, OriginalCurrencyId = 0, OriginalFromAmount = 0 },
                new Transaction { Id = 0, FromAccountId = a, FromAmount = Big, DateTime = 3, OriginalCurrencyId = 0, OriginalFromAmount = 0 },
            });
            await db.RebuildAccountBalanceAsync(a);
            await db.RebuildAccountBalanceAsync(b);

            using var uow2 = db.CreateUnitOfWork();

            var blotter = await uow2.GetRepository<BlotterTransactions>().GetAllAsync();
            Assert.Equal<long?>(Big, blotter.Single(x => x.DateTime == 3).FromAccountBalance);
            var transfer = blotter.Single(x => x.DateTime == 2);
            Assert.Equal<long?>(0, transfer.FromAccountBalance);
            Assert.Equal<long?>(Big, transfer.ToAccountBalance); // the transfer pushed B past 32 bits

            var forAccount = await uow2.GetRepository<BlotterTransactionsForAccountWithSplits>().FindManyAsync(x => x.FromAccountId == b);
            Assert.Equal<long?>(Big, forAccount.Single().FromAccountBalance);
        }
    }
}
