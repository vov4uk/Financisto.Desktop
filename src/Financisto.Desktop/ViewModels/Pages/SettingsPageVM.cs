using System;
using System.Diagnostics;
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
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IFinancistoDatabase db;
        private readonly IToastNotifierWrapper notifier;
        private readonly IDialogWrapper dialogWrapper;

        private readonly UpdateService updateService;
        private IAsyncCommand _refreshDataCommand;
        private IAsyncCommand _saveCommand;
        private IAsyncCommand _browseBackupDirCommand;
        private IAsyncCommand _checkForUpdateCommand;

        private ExchangeRatesProviders _providerSelected;
        private SettingsDto _entity;

        public SettingsPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper, IToastNotifierWrapper notifier, UpdateService updateService)
        {
            this.db = db;
            this.notifier = notifier;
            this.dialogWrapper = dialogWrapper;
            this.updateService = updateService;
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

        public IAsyncCommand BrowseBackupDirCommand => _browseBackupDirCommand ??= new AsyncCommand(BrowseBackupDir);

        public IAsyncCommand CheckForUpdateCommand => _checkForUpdateCommand ??= new AsyncCommand(CheckForUpdatesAsync);

        private async Task BrowseBackupDir()
        {
            var folder = await dialogWrapper.OpenFolderDialogAsync(Entity.General.DefaultBackupDir);
            if (!string.IsNullOrEmpty(folder))
            {
                Entity.General.DefaultBackupDir = folder;
            }
        }

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

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                if (updateService == null)
                    return;

                var updateVersion = await updateService.CheckForUpdatesAsync();
                if (updateVersion is null)
                {
                    notifier.ShowMessage(LocalizationService.Instance.latest_version);
                    return;
                }

                var result = await dialogWrapper.ShowMessageBoxAsync(
                   LocalizationService.Instance.update_available_question,
                   string.Format(LocalizationService.Instance.update_available, updateVersion),
                   true);

                if (result)
                {

                    notifier.ShowMessage(string.Format(LocalizationService.Instance.downloading_update,
                        "Financisto.Desktop",
                        updateVersion));

                    await updateService.PrepareUpdateAsync(updateVersion);

                    notifier.ShowMessage(LocalizationService.Instance.update_downloaded);
                    await Task.Delay(3000);
                    updateService.FinalizeUpdate(true);
                    await Task.Delay(3000);
                    Process.GetCurrentProcess().CloseMainWindow();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.ToString());
                notifier.ShowWarning(LocalizationService.Instance.update_failed);
            }
        }

    }
}
