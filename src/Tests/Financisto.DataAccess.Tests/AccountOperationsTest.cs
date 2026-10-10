namespace Financisto.DataAccess.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Data;
    using Xunit;

    /// <summary>Android's deleteAccount and purgeAccountAtDate.</summary>
    public class AccountOperationsTest
    {
        private const string PreviousPeriod = "Previous period";

        [Fact]
        public async Task DeleteAccountAsync_AccountWithTransfers_RemovesAccountAndKeepsTheOtherBalance()
        {
            var (db, a, b) = await Setup();
            await db.AddTransactionsAsync(new[]
            {
                Tx(a.Id, 10000, Ms(2020, 1, 10)),
                Tx(a.Id, -500, Ms(2020, 2, 10)),
                Tx(b.Id, 5000, Ms(2020, 1, 5)),
                Tx(b.Id, -3000, Ms(2020, 3, 10), to: a.Id, toAmount: 3000), // B -> A
                Tx(a.Id, -1000, Ms(2020, 4, 10), to: b.Id, toAmount: 1000), // A -> B
            });
            await Rebuild(db, a, b);
            Assert.Equal(11500, (await Balances(db))[a.Id]);
            Assert.Equal(3000, (await Balances(db))[b.Id]);

            await db.DeleteAccountAsync(a.Id);

            using var uow = db.CreateUnitOfWork();
            Assert.DoesNotContain(await uow.GetRepository<Account>().GetAllAsync(), x => x.Id == a.Id);

            var transactions = await uow.GetRepository<Transaction>().GetAllAsync();
            Assert.DoesNotContain(transactions, x => x.FromAccountId == a.Id || x.ToAccountId == a.Id);
            Assert.Equal(3, transactions.Count); // B's own, and the two transfers that became plain transactions of B
            Assert.Contains(transactions, x => x.FromAmount == -3000 && x.ToAccountId == 0); // the transfer to A is an expense now
            Assert.Contains(transactions, x => x.FromAmount == 1000 && x.ToAccountId == 0); // the transfer from A is an income now

            Assert.Empty(await uow.GetRepository<RunningBalance>().FindManyAsync(x => x.AccountId == a.Id));

            await db.RebuildAccountBalanceAsync(b.Id);
            Assert.Equal(3000, (await Balances(db))[b.Id]); // unchanged
        }

        [Fact]
        public async Task DeleteAccountAsync_IncomingSplitPart_BecomesPlainTransactionOfTheOtherAccount()
        {
            var (db, a, b) = await Setup();
            var parent = Tx(a.Id, -3000, Ms(2020, 1, 10));
            parent.CategoryId = -1;
            await db.AddTransactionsAsync(new[] { parent });

            // an incoming part is stored on the other account, pointing at the parent of account A
            var part = Tx(b.Id, -2000, Ms(2020, 1, 10), to: a.Id, toAmount: 2000);
            part.ParentId = parent.Id;
            part.ParentAccountId = a.Id;
            await db.AddTransactionsAsync(new[] { part });

            await db.DeleteAccountAsync(a.Id);

            using var uow = db.CreateUnitOfWork();
            var remaining = await uow.GetRepository<Transaction>().GetAllAsync();
            var survivor = Assert.Single(remaining);
            Assert.Equal(b.Id, survivor.FromAccountId);
            Assert.Equal(-2000, survivor.FromAmount);
            Assert.Equal(0, survivor.ToAccountId);
            Assert.Equal(0, survivor.ParentId); // its parent is gone
        }

        [Fact]
        public async Task PurgeAccountAsync_OldTransactions_ReplacedByOneBalanceTransaction()
        {
            var (db, a, b) = await Setup();
            await db.AddTransactionsAsync(new[]
            {
                Tx(a.Id, 10000, Ms(2020, 1, 10)),
                Tx(b.Id, 5000, Ms(2020, 1, 5)),
                Tx(b.Id, -3000, Ms(2020, 3, 10), to: a.Id, toAmount: 3000), // B -> A, the last one before the cut for A
                Tx(a.Id, -1000, Ms(2020, 4, 10), to: b.Id, toAmount: 1000), // A -> B
                Tx(a.Id, -500, Ms(2021, 6, 1)),
            });
            await Rebuild(db, a, b);
            Assert.Equal(11500, (await Balances(db))[a.Id]);
            Assert.Equal(3000, (await Balances(db))[b.Id]);

            await db.PurgeAccountAsync(a.Id, Ms(2020, 12, 31, 0), PreviousPeriod);

            using var uow = db.CreateUnitOfWork();
            var transactions = await uow.GetRepository<Transaction>().FindManyAsync(x => x.FromAccountId == a.Id);
            Assert.Equal(2, transactions.Count);

            var previous = transactions.OrderBy(x => x.DateTime).First();
            Assert.Equal(12000, previous.FromAmount); // 10000 + 3000 incoming - 1000 outgoing: the incoming transfer counts
            Assert.Equal("CL", previous.Status);
            Assert.Equal(EndOfDay(Ms(2020, 4, 10)), previous.DateTime);
            var payee = Assert.Single(await uow.GetRepository<Payee>().FindManyAsync(x => x.Title == PreviousPeriod));
            Assert.Equal(payee.Id, previous.PayeeId);

            var balances = await Balances(db);
            Assert.Equal(11500, balances[a.Id]); // the total doesn't change
            Assert.Equal(3000, balances[b.Id]);

            // B keeps its history: the transfers are plain transactions of B now
            var bTransactions = await uow.GetRepository<Transaction>().FindManyAsync(x => x.FromAccountId == b.Id);
            Assert.Equal(3, bTransactions.Count);
            Assert.All(bTransactions, x => Assert.Equal(0, x.ToAccountId));
        }

        [Fact]
        public async Task PurgeAccountAsync_NothingThatOld_ChangesNothing()
        {
            var (db, a, _) = await Setup();
            await db.AddTransactionsAsync(new[] { Tx(a.Id, 10000, Ms(2021, 1, 10)) });
            await db.RebuildAccountBalanceAsync(a.Id);

            await db.PurgeAccountAsync(a.Id, Ms(2020, 12, 31, 0), PreviousPeriod);

            using var uow = db.CreateUnitOfWork();
            Assert.Single(await uow.GetRepository<Transaction>().GetAllAsync());
            Assert.Empty(await uow.GetRepository<Payee>().FindManyAsync(x => x.Title == PreviousPeriod));
        }

        [Fact]
        public async Task PurgeAccountAsync_ExistingPayee_IsReused()
        {
            var (db, a, _) = await Setup();
            await db.InsertOrUpdateAsync(new[] { new Payee { Id = 0, Title = PreviousPeriod } });
            await db.AddTransactionsAsync(new[] { Tx(a.Id, 10000, Ms(2020, 1, 10)) });
            await db.RebuildAccountBalanceAsync(a.Id);

            await db.PurgeAccountAsync(a.Id, Ms(2020, 12, 31, 0), PreviousPeriod);

            using var uow = db.CreateUnitOfWork();
            Assert.Single(await uow.GetRepository<Payee>().FindManyAsync(x => x.Title == PreviousPeriod));
            Assert.Equal(10000, (await Balances(db))[a.Id]);
        }

        [Fact]
        public async Task PurgeAccountAsync_LaterTransactionsOnTheSameDay_AreDeletedToo()
        {
            var (db, a, _) = await Setup();
            await db.AddTransactionsAsync(new[]
            {
                Tx(a.Id, 10000, Ms(2020, 12, 31, 8)),
                Tx(a.Id, -500, Ms(2020, 12, 31, 23)),
                Tx(a.Id, -100, Ms(2021, 1, 1, 0)),
            });
            await db.RebuildAccountBalanceAsync(a.Id);

            await db.PurgeAccountAsync(a.Id, Ms(2020, 12, 31, 0), PreviousPeriod);

            using var uow = db.CreateUnitOfWork();
            var transactions = (await uow.GetRepository<Transaction>().GetAllAsync()).OrderBy(x => x.DateTime).ToList();
            Assert.Equal(2, transactions.Count);
            Assert.Equal(9500, transactions[0].FromAmount);
            Assert.Equal(-100, transactions[1].FromAmount);
            Assert.Equal(9400, (await Balances(db))[a.Id]);
        }

        private static async Task<(FinancistoDatabase db, Account a, Account b)> Setup()
        {
            var db = new FinancistoDatabase();
            await db.SeedAsync();

            using (var uow = db.CreateUnitOfWork())
            {
                var currency = new Currency { Id = 0, Title = "Dollar", IsDefault = true, IsActive = true, Name = "USD", Decimals = 2, Symbol = "$", SymbolFormat = "." };
                await uow.GetRepository<Currency>().AddAsync(currency);
                await uow.SaveChangesAsync();

                await uow.GetRepository<Account>().AddAsync(new Account { Id = 0, Title = "A", CurrencyId = currency.Id, Type = "CASH", IsActive = true });
                await uow.GetRepository<Account>().AddAsync(new Account { Id = 0, Title = "B", CurrencyId = currency.Id, Type = "CASH", IsActive = true });
                await uow.SaveChangesAsync();
            }

            using (var uow = db.CreateUnitOfWork())
            {
                var accounts = (await uow.GetRepository<Account>().GetAllAsync()).OrderBy(x => x.Title).ToList();
                return (db, accounts[0], accounts[1]);
            }
        }

        private static Transaction Tx(int fromAccountId, long fromAmount, long dateTime, int to = 0, long toAmount = 0) => new Transaction
        {
            Id = 0,
            FromAccountId = fromAccountId,
            FromAmount = fromAmount,
            ToAccountId = to,
            ToAmount = toAmount,
            DateTime = dateTime,
            OriginalCurrencyId = 0,
            OriginalFromAmount = 0,
        };

        private static async Task Rebuild(FinancistoDatabase db, params Account[] accounts)
        {
            foreach (var account in accounts)
            {
                await db.RebuildAccountBalanceAsync(account.Id);
            }
        }

        private static async Task<Dictionary<int, long>> Balances(FinancistoDatabase db)
        {
            using var uow = db.CreateUnitOfWork();
            return (await uow.GetRepository<Account>().GetAllAsync()).ToDictionary(x => x.Id, x => x.TotalAmount);
        }

        private static long Ms(int year, int month, int day, int hour = 12) =>
            new DateTimeOffset(new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Local)).ToUnixTimeMilliseconds();

        private static long EndOfDay(long ms)
        {
            var local = DateTimeOffset.FromUnixTimeMilliseconds(ms).ToLocalTime();
            return new DateTimeOffset(new DateTime(local.Year, local.Month, local.Day, 23, 59, 59, 999, DateTimeKind.Local)).ToUnixTimeMilliseconds();
        }
    }
}
