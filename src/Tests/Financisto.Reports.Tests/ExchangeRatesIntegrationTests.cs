namespace Financisto.Reports.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Xunit;

    /// <summary>
    /// The balances the Saldo and Assets reports convert to the home currency and to USD, run against a real in-memory
    /// database. Only some currency pairs have a stored rate, as in a real backup: a rate that is missing for a pair
    /// has to be found from the inverse pair or through the home currency, and an account whose currency can't be
    /// converted at all must not spoil the totals of the others.
    /// </summary>
    public class ExchangeRatesIntegrationTests : IDisposable
    {
        private const int Uah = 1;
        private const int Usd = 2;
        private const int Eur = 3;
        private const int Pln = 4;

        private static readonly long Jan2026 = Ms(2026, 1, 1);
        private static readonly long Jun2026 = Ms(2026, 6, 1);

        private readonly IFinancistoDatabase db = new FinancistoDatabaseFactory().CreateDatabase();

        public void Dispose()
        {
            this.db.Dispose();
        }

        [Fact]
        public async Task Assets_RateOfThePair_IsUsed()
        {
            await this.Setup();

            var usdAccount = await this.ActivesRow("Dollars", new DateTime(2026, 9, 30));

            Assert.Equal(4200, usdAccount.DefaultCurrencyBalance); // 100 USD at 42, the rate since June
            Assert.Equal(100, usdAccount.UsdBalance);
        }

        [Fact]
        public async Task Assets_SameCurrencyAsTheHomeCurrency_IsNotRounded()
        {
            await this.Setup();

            var homeAccount = await this.ActivesRow("Hryvnias", new DateTime(2026, 9, 30));

            Assert.Equal(1000.55, homeAccount.DefaultCurrencyBalance);
        }

        [Fact]
        public async Task Assets_HistoricalRate_IsTheOneInForceAtTheDate()
        {
            await this.Setup();

            var inMarch = await this.ActivesRow("Dollars", new DateTime(2026, 3, 31));
            var inSeptember = await this.ActivesRow("Dollars", new DateTime(2026, 9, 30));

            Assert.Equal(4000, inMarch.DefaultCurrencyBalance); // 100 USD at 40
            Assert.Equal(4200, inSeptember.DefaultCurrencyBalance); // 100 USD at 42
        }

        [Fact]
        public async Task Assets_DateBeforeTheFirstRate_UsesTheEarliestRate()
        {
            await this.Setup();

            var account = await this.ActivesRow("Dollars", new DateTime(2025, 12, 15));

            Assert.Equal(4000, account.DefaultCurrencyBalance);
        }

        [Fact]
        public async Task Assets_OnlyTheOppositePairIsStored_UsesItsInverse()
        {
            await this.Setup();

            var homeAccount = await this.ActivesRow("Hryvnias", new DateTime(2026, 9, 30));

            // no UAH -> USD rate is stored, only USD -> UAH (42 since June)
            Assert.Equal(24, homeAccount.UsdBalance); // 1000.55 / 42 = 23.8, rounded
        }

        [Fact]
        public async Task Assets_NoRateForThePair_GoesThroughTheHomeCurrency()
        {
            await this.Setup();

            var euroAccount = await this.ActivesRow("Euros", new DateTime(2026, 9, 30));

            Assert.Equal(1000, euroAccount.DefaultCurrencyBalance); // 20 EUR at 50
            Assert.Equal(24, euroAccount.UsdBalance); // 20 EUR = 1000 UAH = 23.8 USD, rounded
        }

        [Fact]
        public async Task Assets_NoWayToConvert_HasNoTotal()
        {
            await this.Setup();

            var zlotyAccount = await this.ActivesRow("Zloty", new DateTime(2026, 9, 30));

            Assert.Null(zlotyAccount.DefaultCurrencyBalance);
            Assert.Null(zlotyAccount.UsdBalance);
        }

        [Fact]
        public async Task Saldo_AccountWithoutRate_DoesNotZeroTheTotalsOfTheOthers()
        {
            await this.Setup();
            var vm = new ReportStructureSaldoVM(this.db);

            await vm.RefreshDataCommand.ExecuteAsync();

            var month = vm.Entities[0];

            // Hryvnias 1000.55 + Dollars 100 * 42 + Euros 20 * 50; the zloty account can't be converted and is left out
            Assert.Equal(6200.55, month.AssetsDefaultCurrencyBalance);

            // Hryvnias 24 + Dollars 100 + Euros 24 (each rounded)
            Assert.Equal(148, month.AssetsUSDBalance);
        }

        [Fact]
        public async Task Saldo_EveryMonthOfTheRange_HasItsTotals()
        {
            await this.Setup();
            var vm = new ReportStructureSaldoVM(this.db) { Range = ReportStructureSaldoRange.Last12Months };

            await vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(12, vm.Entities.Count);
            Assert.All(vm.Entities, x => Assert.True(x.AssetsUSDBalance > 0, $"{x.Date}: {x.AssetsUSDBalance}"));
            Assert.All(vm.Entities, x => Assert.True(x.AssetsDefaultCurrencyBalance > 0, $"{x.Date}: {x.AssetsDefaultCurrencyBalance}"));
        }

        private static long Ms(int year, int month, int day) =>
            new DateTimeOffset(new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Local)).ToUnixTimeMilliseconds();

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

        private async Task<ReportStructureActivesModel> ActivesRow(string accountTitle, DateTime date)
        {
            var vm = new ReportStructureActivesVM(this.db)
            {
                DateFilter = date,
                StartYearMonths = new YearMonths(),
                EndYearMonths = new YearMonths(),
            };

            await vm.RefreshDataCommand.ExecuteAsync();

            return vm.Entities.Single(x => x.Title == accountTitle);
        }

        private async Task Setup()
        {
            var opening = Ms(2025, 1, 1);
            var entities = new List<Entity>
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

                // Only some pairs are stored: USD -> UAH (rising in June) and EUR -> UAH. No rate at all for PLN.
                NewRate(Usd, Uah, Jan2026, 40),
                NewRate(Usd, Uah, Jun2026, 42),
                NewRate(Eur, Uah, Jan2026, 50),
            };

            await this.db.ImportEntitiesAsync(entities);

            DbManual.SetupTests(new List<CurrencyModel> { new CurrencyModel() });
            DbManual.SetupTests(new List<AccountFilterModel> { new AccountFilterModel() });
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel() });
            DbManual.SetupTests(new List<ProjectModel> { new ProjectModel() });
            DbManual.SetupTests(new List<PayeeModel> { new PayeeModel() });
        }
    }
}
