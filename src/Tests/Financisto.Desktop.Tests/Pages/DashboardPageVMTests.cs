namespace Financisto.Desktop.Tests.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels.Pages;
    using Moq;
    using Xunit;

    /// <summary>The net worth chart of the dashboard against a real in-memory database, with only some exchange rates stored.</summary>
    public class DashboardPageVMTests : IDisposable
    {
        private const int Uah = 1;
        private const int Usd = 2;
        private const int Eur = 3;
        private const int Pln = 4;

        private readonly FinancistoDatabase db = new FinancistoDatabase();
        private readonly DateTime today = DateTime.Today;

        public void Dispose()
        {
            this.db.Dispose();
        }

        [Fact]
        public async Task NetWorth_ConvertsEveryMonthWithTheRateInForceAtItsEnd()
        {
            var raisedRateMonth = await this.Setup();
            var vm = new DashboardPageVM(this.db, new Mock<IToastNotifierWrapper>().Object);

            await vm.RefreshDataCommand.ExecuteAsync();

            var netWorth = vm.LineSeries[2].Values.Cast<double>().ToArray();
            var monthEnds = MonthEnds(12);
            Assert.Equal(12, netWorth.Length);
            for (var i = 0; i < netWorth.Length; i++)
            {
                // 100 USD at 40, or at 42 once the rate has been raised
                var dollarRate = monthEnds[i] >= raisedRateMonth ? 42 : 40;

                // Hryvnias 1000.55, Dollars, Euros 20 at 50 (only UAH -> EUR is stored); the zloty account has no rate at all
                Assert.Equal(1000.55 + (100 * dollarRate) + 1000, netWorth[i], 2);
            }
        }

        [Fact]
        public async Task NetWorth_AccountWithoutRate_IsLeftOutInsteadOfBreakingTheChart()
        {
            await this.Setup();
            var vm = new DashboardPageVM(this.db, new Mock<IToastNotifierWrapper>().Object);

            await vm.RefreshDataCommand.ExecuteAsync();

            // the zloty account alone is 500: the chart has to be well below that plus the rest, and never zero
            Assert.All(vm.LineSeries[2].Values.Cast<double>(), x => Assert.True(x > 5000, x.ToString()));
        }

        private static DateTime[] MonthEnds(int count)
        {
            var firstOfNextMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);

            // oldest first, like the chart
            return Enumerable.Range(0, count).Select(i => firstOfNextMonth.AddMonths(-i).AddDays(-1)).OrderBy(x => x).ToArray();
        }

        private static long Ms(DateTime date) => new DateTimeOffset(date.AddHours(12)).ToUnixTimeMilliseconds();

        private static Currency NewCurrency(int id, string name, bool isDefault = false) => new Currency
        {
            Id = id,
            Name = name,
            Title = name,
            Symbol = name,
            IsDefault = isDefault,
            DecimalSeparator = "'.'",
            GroupSeparator = "' '",
        };

        private static Account NewAccount(int id, string title, int currencyId) => new Account
        {
            Id = id,
            Title = title,
            CurrencyId = currencyId,
            SortOrder = id,
        };

        private static Transaction NewDeposit(int id, int accountId, long amount, long dateTime) => new Transaction
        {
            Id = id,
            FromAccountId = accountId,
            FromAmount = amount,
            DateTime = dateTime,
            Status = "UR",
        };

        private static CurrencyExchangeRate NewRate(int from, int to, long date, double rate) => new CurrencyExchangeRate
        {
            FromCurrencyId = from,
            ToCurrencyId = to,
            Date = date,
            Rate = rate,
        };

        /// <summary>Returns the first day of the month in which the dollar rate goes up.</summary>
        private async Task<DateTime> Setup()
        {
            var opening = Ms(new DateTime(2024, 1, 1));
            var firstOfThisMonth = new DateTime(this.today.Year, this.today.Month, 1);
            var firstRate = firstOfThisMonth.AddMonths(-10);
            var raisedRate = firstOfThisMonth.AddMonths(-3);

            await this.db.ImportEntitiesAsync(new List<Financisto.DataAccess.Data.Entity>
            {
                NewCurrency(Uah, "UAH", isDefault: true),
                NewCurrency(Usd, "USD"),
                NewCurrency(Eur, "EUR"),
                NewCurrency(Pln, "PLN"),

                NewAccount(1, "Hryvnias", Uah),
                NewAccount(2, "Dollars", Usd),
                NewAccount(3, "Euros", Eur),
                NewAccount(4, "Zloty", Pln),

                NewDeposit(1, 1, 100055, opening),
                NewDeposit(2, 2, 10000, opening),
                NewDeposit(3, 3, 2000, opening),
                NewDeposit(4, 4, 50000, opening),

                NewRate(Usd, Uah, Ms(firstRate), 40),
                NewRate(Usd, Uah, Ms(raisedRate), 42),
                NewRate(Uah, Eur, Ms(firstRate), 0.02), // only this direction is stored, the euro account needs its inverse
            });

            return raisedRate;
        }
    }
}
