namespace Financisto.Desktop.Tests.Pages.Dialog
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.Common.Utils;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Services;
    using Financisto.Desktop.ViewModels.Pages;
    using Moq;
    using Xunit;

    public class SettingsPageVMTest
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task RefreshData_NullOrEmptyAppId_RemainsUnchanged(string appId)
        {
            var vm = await CreateRefreshedVm(CreateEntity(appId: appId));
            Assert.Equal(appId, vm.Entity.ExchangeRates.OpenExchangeRatesProviderAppId);
        }

        [Fact]
        public async Task RefreshData_PlainTextAppId_IsRetainedAsIs()
        {
            // TryDecrypt falls back to returning the input when it is not a valid DPAPI blob
            var vm = await CreateRefreshedVm(CreateEntity(appId: "plain-text-key"));
            Assert.Equal("plain-text-key", vm.Entity.ExchangeRates.OpenExchangeRatesProviderAppId);
        }

        [Fact]
        public async Task RefreshData_EncryptedAppId_IsDecrypted()
        {
            const string plainText = "my-secret-app-id";
            var vm = await CreateRefreshedVm(CreateEntity(provider: ExchangeRatesProviders.OpenExchangeRates, appId: SettingsProtection.Encrypt(plainText)));
            Assert.Equal(plainText, vm.Entity.ExchangeRates.OpenExchangeRatesProviderAppId);
        }

        [Fact]
        public async Task RefreshData_EntityIsCloneOfCurrentSettings()
        {
            var settings = CreateEntity(provider: ExchangeRatesProviders.Monobank);
            var vm = await CreateRefreshedVm(settings);

            Assert.NotSame(settings, vm.Entity);
            Assert.Equal(settings.ExchangeRates.Provider, vm.Entity.ExchangeRates.Provider);
        }

        [Theory]
        [InlineData(ExchangeRatesProviders.None)]
        [InlineData(ExchangeRatesProviders.Monobank)]
        [InlineData(ExchangeRatesProviders.OpenExchangeRates)]
        [InlineData(ExchangeRatesProviders.FreeCurrencyRates)]
        public async Task RefreshData_SetsSelectedProvider_FromSettings(ExchangeRatesProviders provider)
        {
            var vm = await CreateRefreshedVm(CreateEntity(provider: provider));
            Assert.Equal(provider, vm.SelectedProvider);
        }

        [Theory]
        [InlineData(ExchangeRatesProviders.None)]
        [InlineData(ExchangeRatesProviders.Monobank)]
        [InlineData(ExchangeRatesProviders.FreeCurrencyRates)]
        public async Task IsOpenExchangeRatesProviderSelected_WhenNotOpenExchangeRates_ReturnsFalse(ExchangeRatesProviders provider)
        {
            var vm = await CreateRefreshedVm(CreateEntity(provider: provider));
            Assert.False(vm.IsOpenExchangeRatesProviderSelected);
        }

        [Fact]
        public async Task IsOpenExchangeRatesProviderSelected_WhenOpenExchangeRates_ReturnsTrue()
        {
            var vm = await CreateRefreshedVm(CreateEntity(provider: ExchangeRatesProviders.OpenExchangeRates));
            Assert.True(vm.IsOpenExchangeRatesProviderSelected);
        }

        [Fact]
        public async Task Save_StoresCloneOfEntityInCurrentSettings()
        {
            var vm = await CreateRefreshedVm(CreateEntity());

            await vm.SaveCommand.ExecuteAsync();

            Assert.NotSame(vm.Entity, SettingsService.Current.Settings);
        }

        [Theory]
        [InlineData(ExchangeRatesProviders.None)]
        [InlineData(ExchangeRatesProviders.Monobank)]
        [InlineData(ExchangeRatesProviders.OpenExchangeRates)]
        [InlineData(ExchangeRatesProviders.FreeCurrencyRates)]
        public async Task Save_SetsProvider_FromSelectedProvider(ExchangeRatesProviders provider)
        {
            var vm = await CreateRefreshedVm(CreateEntity());
            vm.SelectedProvider = provider;

            await vm.SaveCommand.ExecuteAsync();

            Assert.Equal(provider, vm.Entity.ExchangeRates.Provider);
            Assert.Equal(provider, SettingsService.Current.Settings.ExchangeRates.Provider);
        }

        [Theory]
        [InlineData(ExchangeRatesProviders.None)]
        [InlineData(ExchangeRatesProviders.Monobank)]
        [InlineData(ExchangeRatesProviders.FreeCurrencyRates)]
        public async Task Save_WithNonOpenExchangeRatesProvider_ClearsAppId(ExchangeRatesProviders provider)
        {
            var vm = await CreateRefreshedVm(CreateEntity(provider: provider, appId: "some-api-key"));
            vm.SelectedProvider = provider;

            await vm.SaveCommand.ExecuteAsync();

            Assert.Equal(string.Empty, vm.Entity.ExchangeRates.OpenExchangeRatesProviderAppId);
            Assert.Equal(string.Empty, SettingsService.Current.Settings.ExchangeRates.OpenExchangeRatesProviderAppId);
        }

        [Fact]
        public async Task Save_WithOpenExchangeRatesProviderAndEmptyAppId_LeavesAppIdEmpty()
        {
            var vm = await CreateRefreshedVm(CreateEntity(provider: ExchangeRatesProviders.OpenExchangeRates, appId: string.Empty));

            await vm.SaveCommand.ExecuteAsync();

            Assert.Equal(string.Empty, SettingsService.Current.Settings.ExchangeRates.OpenExchangeRatesProviderAppId);
        }

        [Fact]
        public async Task Save_WithOpenExchangeRatesProviderAndNonEmptyAppId_EncryptsSavedAppId()
        {
            const string plainText = "my-secret-app-id";
            var vm = await CreateRefreshedVm(CreateEntity(provider: ExchangeRatesProviders.OpenExchangeRates, appId: plainText));

            await vm.SaveCommand.ExecuteAsync();

            var saved = SettingsService.Current.Settings.ExchangeRates.OpenExchangeRatesProviderAppId;
            Assert.NotEqual(plainText, saved);
            Assert.NotEmpty(saved);
            Assert.Equal(plainText, SettingsProtection.TryDecrypt(saved));
        }

        [Fact]
        public async Task Save_WithOpenExchangeRatesProviderAndNonEmptyAppId_KeepsEntityAppIdPlain()
        {
            // Entity stays bound to the UI, so saving twice must not encrypt twice.
            const string plainText = "my-secret-app-id";
            var vm = await CreateRefreshedVm(CreateEntity(provider: ExchangeRatesProviders.OpenExchangeRates, appId: plainText));

            await vm.SaveCommand.ExecuteAsync();

            Assert.Equal(plainText, vm.Entity.ExchangeRates.OpenExchangeRatesProviderAppId);
        }

        [Fact]
        public async Task Save_ShowsSavedMessage()
        {
            var notifierMock = new Mock<IToastNotifierWrapper>();
            var vm = await CreateRefreshedVm(CreateEntity(), notifierMock);

            await vm.SaveCommand.ExecuteAsync();

            notifierMock.Verify(x => x.ShowMessage(It.IsAny<string>()), Times.Once);
        }

        [Theory]
        [InlineData(IconSetType.Default)]
        [InlineData(IconSetType.Monocolor)]
        public async Task RefreshData_LoadsIconSet_FromSettings(IconSetType iconSet)
        {
            var vm = await CreateRefreshedVm(CreateEntity(iconSet: iconSet));

            Assert.Equal(iconSet, vm.Entity.General.IconSet);
        }

        [Theory]
        [InlineData(IconSetType.Default, IconSetType.Monocolor)]
        [InlineData(IconSetType.Monocolor, IconSetType.Default)]
        public async Task Save_ChangedIconSet_IsStoredAndAppliedToIconSettings(IconSetType before, IconSetType after)
        {
            IconSettings.Instance.IconSet = before;
            try
            {
                var vm = await CreateRefreshedVm(CreateEntity(iconSet: before));
                var raised = new List<string>();
                IconSettings.Instance.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
                vm.Entity.General.IconSet = after;

                await vm.SaveCommand.ExecuteAsync();

                Assert.Equal(after, SettingsService.Current.Settings.General.IconSet);
                Assert.Equal(after, IconSettings.Instance.IconSet);
                Assert.Contains(nameof(IconSettings.IconSet), raised);
            }
            finally
            {
                IconSettings.Instance.IconSet = IconSetType.Default;
            }
        }

        [Fact]
        public async Task Save_UnchangedIconSet_DoesNotRaiseIconSettingsChanged()
        {
            IconSettings.Instance.IconSet = IconSetType.Monocolor;
            try
            {
                var vm = await CreateRefreshedVm(CreateEntity(iconSet: IconSetType.Monocolor));
                var raised = new List<string>();
                IconSettings.Instance.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

                await vm.SaveCommand.ExecuteAsync();

                Assert.Empty(raised);
            }
            finally
            {
                IconSettings.Instance.IconSet = IconSetType.Default;
            }
        }

        [Fact]
        public async Task Save_SameLanguage_DbManualNotRefreshed()
        {
            var dbMock = await SetupDbManual();
            var vm = await CreateRefreshedVm(CreateEntity(language: Language.English), dbMock: dbMock);
            dbMock.Invocations.Clear();

            await vm.SaveCommand.ExecuteAsync();

            dbMock.Verify(x => x.ExecuteQuery<CurrencyModel>(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Save_DifferentLanguage_DbManualRefreshed()
        {
            var dbMock = await SetupDbManual();
            var vm = await CreateRefreshedVm(CreateEntity(language: Language.English), dbMock: dbMock);
            vm.Entity.General.Language = Language.Ukrainian;
            dbMock.Invocations.Clear();

            try
            {
                await vm.SaveCommand.ExecuteAsync();

                Assert.Equal(Language.Ukrainian, SettingsService.Current.Settings.General.Language);
                dbMock.Verify(x => x.ExecuteQuery<CurrencyModel>(It.IsAny<string>()), Times.Once);
            }
            finally
            {
                LocalizationService.Instance.ApplyLanguage(Language.English);
            }
        }

        [Fact]
        public async Task BrowseBackupDir_FolderSelected_UpdatesDefaultBackupDir()
        {
            var dialogMock = new Mock<IDialogWrapper>();
            dialogMock.Setup(x => x.OpenFolderDialogAsync(It.IsAny<string>())).ReturnsAsync(@"C:\Backups");
            var vm = await CreateRefreshedVm(CreateEntity(), dialogMock: dialogMock);

            await vm.BrowseBackupDirCommand.ExecuteAsync();

            Assert.Equal(@"C:\Backups", vm.Entity.General.DefaultBackupDir);
        }

        [Fact]
        public async Task BrowseBackupDir_DialogCancelled_KeepsDefaultBackupDir()
        {
            var dialogMock = new Mock<IDialogWrapper>();
            dialogMock.Setup(x => x.OpenFolderDialogAsync(It.IsAny<string>())).ReturnsAsync(string.Empty);
            var settings = CreateEntity();
            settings.General.DefaultBackupDir = @"C:\Old";
            var vm = await CreateRefreshedVm(settings, dialogMock: dialogMock);

            await vm.BrowseBackupDirCommand.ExecuteAsync();

            Assert.Equal(@"C:\Old", vm.Entity.General.DefaultBackupDir);
        }

        [Fact]
        public void SelectedProvider_WhenChanged_RaisesPropertyChangedForSelfAndIsOpenExchangeRatesProviderSelected()
        {
            var vm = CreateVm();
            var raised = new List<string>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            vm.SelectedProvider = ExchangeRatesProviders.OpenExchangeRates;

            Assert.Contains(nameof(vm.SelectedProvider), raised);
            Assert.Contains(nameof(vm.IsOpenExchangeRatesProviderSelected), raised);
        }

        [Fact]
        public void SelectedProvider_WhenSetToSameValue_DoesNotRaisePropertyChanged()
        {
            var vm = CreateVm();
            vm.SelectedProvider = ExchangeRatesProviders.Monobank;
            var raised = new List<string>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            vm.SelectedProvider = ExchangeRatesProviders.Monobank;

            Assert.Empty(raised);
        }

        private static SettingsPageVM CreateVm(
            Mock<IToastNotifierWrapper> notifierMock = null,
            Mock<IDialogWrapper> dialogMock = null,
            Mock<IFinancistoDatabase> dbMock = null)
        {
            return new SettingsPageVM(
                (dbMock ?? new Mock<IFinancistoDatabase>()).Object,
                (dialogMock ?? new Mock<IDialogWrapper>()).Object,
                (notifierMock ?? new Mock<IToastNotifierWrapper>()).Object,
                null);
        }

        private static async Task<SettingsPageVM> CreateRefreshedVm(
            SettingsDto settings,
            Mock<IToastNotifierWrapper> notifierMock = null,
            Mock<IDialogWrapper> dialogMock = null,
            Mock<IFinancistoDatabase> dbMock = null)
        {
            SettingsService.Current.Settings = settings;
            var vm = CreateVm(notifierMock, dialogMock, dbMock);
            await vm.RefreshDataCommand.ExecuteAsync();
            return vm;
        }

        private static async Task<Mock<IFinancistoDatabase>> SetupDbManual()
        {
            var dbMock = new Mock<IFinancistoDatabase>();
            dbMock.Setup(x => x.ExecuteQuery<AccountFilterModel>(It.IsAny<string>())).ReturnsAsync(new List<AccountFilterModel> { new AccountFilterModel() });
            dbMock.Setup(x => x.ExecuteQuery<CategoryModel>(It.IsAny<string>())).ReturnsAsync(new List<CategoryModel> { new CategoryModel() });
            dbMock.Setup(x => x.ExecuteQuery<CurrencyModel>(It.IsAny<string>())).ReturnsAsync(new List<CurrencyModel> { new CurrencyModel() });
            dbMock.Setup(x => x.ExecuteQuery<PayeeModel>(It.IsAny<string>())).ReturnsAsync(new List<PayeeModel> { new PayeeModel() });
            dbMock.Setup(x => x.ExecuteQuery<ProjectModel>(It.IsAny<string>())).ReturnsAsync(new List<ProjectModel> { new ProjectModel() });
            dbMock.Setup(x => x.ExecuteQuery<TagModel>(It.IsAny<string>())).ReturnsAsync(new List<TagModel> { new TagModel() });
            dbMock.Setup(x => x.ExecuteQuery<YearMonths>(It.IsAny<string>())).ReturnsAsync(new List<YearMonths> { new YearMonths() });
            dbMock.Setup(x => x.ExecuteQuery<Years>(It.IsAny<string>())).ReturnsAsync(new List<Years> { new Years() });
            dbMock.Setup(x => x.ExecuteQuery<LocationModel>(It.IsAny<string>())).ReturnsAsync(new List<LocationModel> { new LocationModel() });

            // DbManual reads the import rules from sms_template
            var rulesRepo = new Mock<IBaseRepository<SmsTemplate>>();
            rulesRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<SmsTemplate>());
            var uowMock = new Mock<IUnitOfWork>();
            uowMock.Setup(x => x.GetRepository<SmsTemplate>()).Returns(rulesRepo.Object);
            dbMock.Setup(x => x.CreateUnitOfWork()).Returns(uowMock.Object);

            DbManual.ResetAllDatabaseManuals();
            await DbManual.SetupAsync(dbMock.Object);
            return dbMock;
        }

        private static SettingsDto CreateEntity(
            ExchangeRatesProviders provider = ExchangeRatesProviders.None,
            string appId = "",
            Language language = Language.English,
            IconSetType iconSet = IconSetType.Default) =>
            new SettingsDto
            {
                General = new SettingsGeneralDto { Language = language, IconSet = iconSet },
                ExchangeRates = new SettingsExchangeRates
                {
                    Provider = provider,
                    OpenExchangeRatesProviderAppId = appId,
                },
            };
    }
}
