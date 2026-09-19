using System.Threading.Tasks;
using Avalonia;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.DataAccess.Abstractions;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Services;
using Prism.Mvvm;

namespace Financisto.Desktop.ViewModels.Pages
{
    public class SettingsPageVM : BindableBase, IDataRefresh
    {
        private readonly IFinancistoDatabase db;
        private readonly IToastNotifierWrapper notifier;
        private IAsyncCommand _refreshDataCommand;
        private IAsyncCommand _saveCommand;
        private ExchangeRatesProviders _providerSelected;
        private SettingsDto _entity;

        public SettingsPageVM(IFinancistoDatabase db, IToastNotifierWrapper notifier)
        {
            this.db = db;
            this.notifier = notifier;
        }

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

        public SettingsDto Entity
        {
            get => _entity;
            private set => SetProperty(ref _entity, value);
        }

        public IAsyncCommand RefreshDataCommand => _refreshDataCommand ??= new AsyncCommand(RefreshData);

        public IAsyncCommand SaveCommand => _saveCommand ??= new AsyncCommand(Save);

        private Task RefreshData()
        {
            Entity = SettingsService.Current.Settings.Clone() is SettingsDto clone ? clone : new SettingsDto();
            SelectedProvider = Entity.ExchangeRates.Provider;
            Entity.ExchangeRates.OpenExchangeRatesProviderAppId = SettingsProtection.TryDecrypt(Entity.ExchangeRates.OpenExchangeRatesProviderAppId);
            return Task.CompletedTask;
        }

        private async Task Save()
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

            Language before = SettingsService.Current.Settings.General.Language;
            SettingsService.Current.Settings = Entity;
            SettingsService.Current.Save();

            if (before != Entity.General.Language)
            {
                LocalizationService.Instance.ApplyLanguage(Entity.General.Language);

                DbManual.ResetManuals(nameof(DbManual.MCCEnums));
                DbManual.ResetManuals(nameof(DbManual.MCCTitles));
                DbManual.ResetManuals(nameof(DbManual.Currencies));
                await DbManual.SetupAsync(db);
            }

            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = Entity.General.ThemeVariant;
            }

            notifier?.ShowMessage(string.Format(LocalizationService.Instance.saved_message, LocalizationService.Instance.settings));
        }
    }
}
