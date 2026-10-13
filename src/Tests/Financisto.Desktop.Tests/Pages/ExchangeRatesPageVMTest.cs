namespace Financisto.Desktop.Tests.Pages
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Services;
    using Financisto.Desktop.ViewModels.Pages;
    using Moq;
    using Xunit;

    [Collection("Integration tests")]
    public class ExchangeRatesPageVMTest
    {
        private static CurrencyExchangeRate Rate(int from, int to, long date) => new CurrencyExchangeRate { FromCurrencyId = from, ToCurrencyId = to, Date = date, Rate = 0.25 };

        private static async Task<IFinancistoDatabase> CreateDbWithRatesAsync()
        {
            var db = new FinancistoDatabaseFactory().CreateDatabase();
            await db.ImportEntitiesAsync(new Entity[]
            {
                new Currency { Id = 1, Title = "Zloty", Name = "PLN", Symbol = "zl", IsDefault = true, IsActive = true },
                new Currency { Id = 2, Title = "Dollar", Name = "USD", Symbol = "$", IsActive = true },
                Rate(1, 2, 100),
                Rate(1, 2, 50),
            });
            return db;
        }

        [Fact]
        public async Task WithoutKnownAsync_KnownKeysAndDuplicatesLeftOut()
        {
            using var db = await CreateDbWithRatesAsync();
            using var uow = db.CreateUnitOfWork();
            var repository = uow.GetRepository<CurrencyExchangeRate>();

            var result = await ExchangeRatesPageVM.WithoutKnownAsync(repository, new[]
            {
                Rate(1, 2, 100),   // the database has it
                Rate(2, 1, 100),   // the other direction: new
                Rate(1, 2, 200),   // a new day: new
                Rate(1, 2, 200),   // the same key again in the batch
            });

            Assert.Equal(new[] { (2, 1, 100L), (1, 2, 200L) }, result.Select(x => (x.FromCurrencyId, x.ToCurrencyId, x.Date)));
        }

        [Fact]
        public async Task WithoutKnownAsync_SecondRefreshOfTheSameBatch_NothingNew()
        {
            using var db = await CreateDbWithRatesAsync();
            var batch = new[] { Rate(1, 2, 300), Rate(2, 1, 300) };
            using (var uow = db.CreateUnitOfWork())
            {
                var repository = uow.GetRepository<CurrencyExchangeRate>();
                await repository.AddRangeAsync(await ExchangeRatesPageVM.WithoutKnownAsync(repository, batch));
                await uow.SaveChangesAsync();
            }

            using var second = db.CreateUnitOfWork();
            var again = await ExchangeRatesPageVM.WithoutKnownAsync(second.GetRepository<CurrencyExchangeRate>(), batch);

            Assert.Empty(again);
        }

        [Fact]
        public async Task WithoutKnownAsync_OnlyTheDaysOfTheBatchAreRead()
        {
            // an older day with the same pair is no conflict, and a day nobody asks about is not touched
            using var db = await CreateDbWithRatesAsync();
            using var uow = db.CreateUnitOfWork();

            var result = await ExchangeRatesPageVM.WithoutKnownAsync(uow.GetRepository<CurrencyExchangeRate>(), new[] { Rate(1, 2, 75) });

            Assert.Single(result);
        }

        [Fact]
        public async Task WithoutKnownAsync_EmptyBatch_Empty()
        {
            using var db = await CreateDbWithRatesAsync();
            using var uow = db.CreateUnitOfWork();

            Assert.Empty(await ExchangeRatesPageVM.WithoutKnownAsync(uow.GetRepository<CurrencyExchangeRate>(), new List<CurrencyExchangeRate>()));
        }

        [Fact]
        public async Task RefreshExchangeRatesCommand_ProviderNone_ShowsWarning()
        {
            SettingsService.Current.Settings = new SettingsDto
            {
                ExchangeRates = new SettingsExchangeRates { Provider = ExchangeRatesProviders.None },
            };
            var notifierMock = new Mock<IToastNotifierWrapper>();
            var vm = new ExchangeRatesPageVM(
                new Mock<IFinancistoDatabase>().Object,
                new Mock<IDialogWrapper>().Object,
                notifierMock.Object);

            await vm.RefreshExchangeRatesCommand.ExecuteAsync();

            notifierMock.Verify(x => x.ShowWarning(LocalizationService.Instance.exchange_rates_provider_not_configured), Times.Once);
        }
    }
}
