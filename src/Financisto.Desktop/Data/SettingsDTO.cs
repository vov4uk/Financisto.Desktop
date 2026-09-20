using System;
using Avalonia.Styling;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Prism.Mvvm;

namespace Financisto.Desktop.Data
{
    public class SettingsDto : BindableBase, ICloneable
    {
        public SettingsGeneralDto General { get; set; } = new SettingsGeneralDto();
        public SettingsExchangeRates ExchangeRates { get; set; } = new SettingsExchangeRates();

        public object Clone()
        {
            var clone = new SettingsDto
            {
                General = (SettingsGeneralDto)General.Clone() ?? new SettingsGeneralDto(),
                ExchangeRates = (SettingsExchangeRates)ExchangeRates.Clone() ?? new SettingsExchangeRates()
            };
            return clone;
        }
    }

    public class SettingsGeneralDto : BindableBase, ICloneable
    {
        private bool checkForUpdatesOnStart;
        private Language language;

        private AppThemeType currentAppTheme;

        private string defaultBackupDir;

        public bool CheckForUpdatesOnStart
        {
            get => checkForUpdatesOnStart;
            set
            {
                if (checkForUpdatesOnStart != value)
                {
                    checkForUpdatesOnStart = value;
                    RaisePropertyChanged(nameof(CheckForUpdatesOnStart));
                }
            }
        }

        public string DefaultBackupDir
        {
            get => defaultBackupDir;
            set
            {
                if (defaultBackupDir != value)
                {
                    defaultBackupDir = value;
                    RaisePropertyChanged(nameof(DefaultBackupDir));
                }
            }
        }

        public Language Language
        {
            get => language;
            set
            {
                if (language != value)
                {
                    language = value;
                    RaisePropertyChanged(nameof(Language));
                }
            }
        }

        public AppThemeType CurrentAppTheme
        {
            get => currentAppTheme;
            set
            {
                if (currentAppTheme != value)
                {
                    currentAppTheme = value;
                    RaisePropertyChanged(nameof(CurrentAppTheme));
                }
            }
        }

        public ThemeVariant ThemeVariant
        {
            get => CurrentAppTheme switch
            {
                AppThemeType.Dark => ThemeVariant.Dark,
                AppThemeType.Light => ThemeVariant.Light,
                AppThemeType.System => ThemeVariant.Default,
                _ => ThemeVariant.Light
            };
        }

        public object Clone()
        {
            return new SettingsGeneralDto
            {
                CheckForUpdatesOnStart = CheckForUpdatesOnStart,
                Language = Language,
                CurrentAppTheme = CurrentAppTheme,
                DefaultBackupDir = DefaultBackupDir
            };
        }
    }

    public class SettingsExchangeRates: BindableBase, ICloneable
    {
        private ExchangeRatesProviders exchangeRatesProvider;
        private string openExchangeRatesProviderAppId;
        private bool updateOnStart;

        public ExchangeRatesProviders Provider
        {
            get => exchangeRatesProvider;
            set
            {
                if (exchangeRatesProvider != value)
                {
                    exchangeRatesProvider = value;
                    RaisePropertyChanged(nameof(Provider));
                }
            }
        }
        public string OpenExchangeRatesProviderAppId
        {
            get => openExchangeRatesProviderAppId;
            set
            {
                if (openExchangeRatesProviderAppId != value)
                {
                    openExchangeRatesProviderAppId = value;
                    RaisePropertyChanged(nameof(OpenExchangeRatesProviderAppId));
                }
            }
        }

        public bool UpdateOnStart
        {
            get => updateOnStart;
            set
            {
                if (updateOnStart != value)
                {
                    updateOnStart = value;
                    RaisePropertyChanged(nameof(UpdateOnStart));
                }
            }
        }

        public object Clone()
        {
            return new SettingsExchangeRates
            {
                Provider = Provider,
                OpenExchangeRatesProviderAppId = OpenExchangeRatesProviderAppId,
                UpdateOnStart = UpdateOnStart
            };
        }
    }
}
