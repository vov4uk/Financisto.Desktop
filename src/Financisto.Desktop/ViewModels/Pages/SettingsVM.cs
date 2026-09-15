using Financisto.Common.Entities;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Prism.Mvvm;

namespace Financisto.Desktop.ViewModels.Pages
{
    public class SettingsVM : BindableBase
    {
        ExchangeRatesProviders _providerSelected;

        public bool IsOpenExchangeRatesProviderSelected
        {
            get => SelectedProvider == ExchangeRatesProviders.OpenExchangeRates;
        }

        public ExchangeRatesProviders SelectedProvider
        {
            get => _providerSelected;
            set
            {
                if (SetProperty(ref _providerSelected, value))
                {
                    RaisePropertyChanged(nameof(IsOpenExchangeRatesProviderSelected));
                }
            }
        }

        public SettingsVM(SettingsDto entity)
        {
            this.Entity = entity;
            this.SelectedProvider = entity.ExchangeRates.Provider;
            Entity.ExchangeRates.OpenExchangeRatesProviderAppId = SettingsProtection.TryDecrypt(Entity.ExchangeRates.OpenExchangeRatesProviderAppId);
        }

        public SettingsDto Entity { get; }

        public object OnRequestSave()
        {
            Entity.ExchangeRates.Provider = SelectedProvider;

            if (!IsOpenExchangeRatesProviderSelected)
            {
                Entity.ExchangeRates.OpenExchangeRatesProviderAppId = "";
            }
            else if (!string.IsNullOrEmpty(Entity.ExchangeRates.OpenExchangeRatesProviderAppId))
            {
                Entity.ExchangeRates.OpenExchangeRatesProviderAppId = SettingsProtection.Encrypt(Entity.ExchangeRates.OpenExchangeRatesProviderAppId);
            }
            return Entity;
        }
    }
}
