namespace Financisto.Desktop.Tests.Pages
{
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.DataAccess.Abstractions;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Services;
    using Financisto.Desktop.ViewModels.Pages;
    using Moq;
    using Xunit;

    public class ExchangeRatesPageVMTest
    {
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
