using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Adapter;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.DataAccess.Utils;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Services;
using Financisto.Desktop.ViewModels.Pages;
using Prism.Mvvm;
using IAsyncCommand = Financisto.Common.IAsyncCommand;

namespace Financisto.Desktop.ViewModels
{
    public class MainWindowVM : BindableBase
    {
        private const string Backup = "backup";
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<Type, BindableBase> _pages = new ConcurrentDictionary<Type, BindableBase>();
        private readonly IFinancistoDatabaseFactory dbFactory;
        private readonly IDialogWrapper dialogWrapper;
        private readonly List<Entity> keyLessEntities = new();
        private readonly IToastNotifierWrapper notifier;
        private readonly UpdateService updateService;
        private BackupVersion _backupVersion;
        private Dictionary<string, List<string>> _entityColumnsOrder;
        private IAsyncCommand<Type> _menuNavigateCommand;
        private IAsyncCommand _openBackupCommand;
        private IAsyncCommand _saveBackupCommand;
        private IAsyncCommand _saveBackupAsDbCommand;
        private IAsyncCommand _checkForUpdateCommand;

        private IAsyncCommand _openPanelCommand;
        private readonly IBackupWriter backupWriter;
        private BindableBase currentPage;
        private IFinancistoDatabase db;
        private readonly IEntityReader entityReader;
        private string openBackupPath;
        private string defaultBackupDirectory;
        private bool isLoading;

        private bool isPanelOpen = true;

        private ListItemTemplate? selectedItemBottom;
        private ListItemTemplate? selectedItemTop;
        public MainWindowVM(IDialogWrapper dialogWrapper,
            IFinancistoDatabaseFactory dbFactory,
            IEntityReader entityReader,
            IBackupWriter backupWriter,
            IToastNotifierWrapper notifier,
            UpdateService updateService)
        {
            this.dialogWrapper = dialogWrapper;
            this.dbFactory = dbFactory;
            this.entityReader = entityReader;
            this.backupWriter = backupWriter;
            this.notifier = notifier;
            this.updateService = updateService;
            db = dbFactory.CreateDatabase();
        }

        public BindableBase CurrentPage
        {
            get => currentPage;
            private set
            {
                SetProperty(ref currentPage, value, nameof(CurrentPage));
                Logger.Info($"CurrentPage -> {value?.GetType().FullName}");
            }
        }

        public string OpenBackupPath
        {
            get => openBackupPath;
            private set => SetProperty(ref openBackupPath, value);
        }
        public string DefaultBackupDirectory
        {
            get => defaultBackupDirectory;
            internal set => SetProperty(ref defaultBackupDirectory, value);
        }

        public bool IsLoading
        {
            get => isLoading;
            private set => SetProperty(ref isLoading, value);
        }
        public ListItemTemplate? SelectedItemBottom
        {
            get => selectedItemBottom;
            set
            {
                SetProperty(ref selectedItemBottom, value);
                if (value != null)
                {
                    SelectedItemTop = null;

                    Task.Run(() => NavigateToType(value.ModelType));
                }
            }
        }

        public ListItemTemplate? SelectedItemTop
        {
            get => selectedItemTop;
            set
            {
                SetProperty(ref selectedItemTop, value);
                if (value != null)
                {
                    SelectedItemBottom = null;

                    Task.Run(() => NavigateToType(value.ModelType));
                }
            }
        }

        public bool IsPanelOpen
        {
            get => isPanelOpen;
            private set => SetProperty(ref isPanelOpen, value);
        }

        public ObservableCollection<ListItemTemplate> ItemsTop { get; } = new()
        {
            //new(typeof(DashboardPageViewModel), "Dashboard", "glance_regular"),
            new(typeof(AccountModel), () => LocalizationService.Instance.accounts, "IconWallet"),
            new(typeof(CategoryTreeModel), () => LocalizationService.Instance.categories, "IconFolderTree"),
            new(typeof(ProjectModel), () => LocalizationService.Instance.projects, "IconListCheck"),
            new(typeof(PayeeModel), () => LocalizationService.Instance.payees, "IconAddressBook"),
            new(typeof(LocationModel), () => LocalizationService.Instance.locations, "IconMap"),
            new(typeof(CurrencyModel), () => LocalizationService.Instance.currencies, "IconDollarSign"),
            new(typeof(ExchangeRateModel), () => LocalizationService.Instance.exchange_rates, "IconArrowTrendUp"),
            new(typeof(BlotterModel), () => LocalizationService.Instance.blotter, "IconReceipt"),
            //new(typeof(ReportsVM), "Reports", "book_pulse_regular"),
        };

        public ObservableCollection<ListItemTemplate> ItemsBottom { get; } = new()
        {
            new(typeof(SettingsVM), () => LocalizationService.Instance.settings, "IconGear"),
        };

        public IAsyncCommand<Type> MenuNavigateCommand => _menuNavigateCommand ??= new AsyncCommand<Type>(NavigateToType);

        public IAsyncCommand OpenBackupCommand => _openBackupCommand ??= new AsyncCommand(OpenBackup_Click);

        public IAsyncCommand SaveBackupCommand => _saveBackupCommand ??= new AsyncCommand(SaveBackup_Click);

        public IAsyncCommand SaveBackupAsDbCommand => _saveBackupAsDbCommand ??= new AsyncCommand(SaveBackupAsDb);

        public IAsyncCommand CheckForUpdateCommand => _checkForUpdateCommand ??= new AsyncCommand(CheckForUpdatesAsync);

        public IAsyncCommand OpenPanelCommand => _openPanelCommand ??= new AsyncCommand(OpenPanelAsync);

        private Task OpenPanelAsync()
        {
            IsPanelOpen = !IsPanelOpen;
            return Task.CompletedTask;
        }

        public async Task OpenBackup(string backupPath)
        {
            try
            {
                OpenBackupPath = backupPath;
                IsLoading = true;
                _pages.Clear();
                Stopwatch stopwatch = Stopwatch.StartNew();
                var (entities, backupVersion, columnsOrder) = await entityReader.ParseBackupFileAsync(backupPath);
                entities = entities as IReadOnlyCollection<Entity> ?? entities.ToList();
                _backupVersion = backupVersion;
                _entityColumnsOrder = columnsOrder;

                db?.Dispose();
                db = dbFactory.CreateDatabase();
                await db.ImportEntitiesAsync(entities);

                keyLessEntities.Clear();

                AddKeylessEntities(entities.OfType<CCardClosingDate>());
                AddKeylessEntities(entities.OfType<CategoryAttribute>());
                AddKeylessEntities(entities.OfType<TransactionAttribute>());

                IsLoading = false;

                DbManual.ResetAllDatabaseManuals();
                await DbManual.SetupAsync(db);

                stopwatch.Stop();
                int entitiesCount = entities?.Count() ?? 0;
                Logger.Info($"Backup loaded in {stopwatch.ElapsedMilliseconds} ms. Backup version : {_backupVersion}. Entities count : {entitiesCount}");

                await NavigateToType(typeof(BlotterModel));

                notifier?.ShowMessage(string.Format(LocalizationService.Instance.entities_loaded, entitiesCount));

                if (SettingsService.Current.Settings?.ExchangeRates.UpdateOnStart == true)
                {
                    var exchangeRatesVM = _pages.GetOrAdd(typeof(ExchangeRateModel), _ => new ExchangeRatesVM(db, dialogWrapper, notifier!)) as ExchangeRatesVM;
                    await exchangeRatesVM?.RefreshExchangeRatesCommand?.ExecuteAsync()!;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.ToString());
                IsLoading = false;
                throw;
            }
        }

        public async Task SaveBackup(string backupPath)
        {
            List<Entity> itemsToBackup = [.. keyLessEntities];
            using (IUnitOfWork uow = db.CreateUnitOfWork())
            {
                itemsToBackup.AddRange(await uow.GetAllAsync<Budget>());
                itemsToBackup.AddRange(await uow.GetAllAsync<TransactionAttribute>());
                itemsToBackup.AddRange(await uow.GetAllAsync<CurrencyExchangeRate>());
                itemsToBackup.AddRange(await uow.GetAllAsync<Currency>());
                itemsToBackup.AddRange((await uow.GetAllAsync<Location>()).Where(x => x.Id > 0));
                itemsToBackup.AddRange(await uow.GetAllAsync<Payee>());
                itemsToBackup.AddRange((await uow.GetAllAsync<Project>()).Where(x => x.Id > 0));
                itemsToBackup.AddRange(await uow.GetAllAsync<Transaction>());
                itemsToBackup.AddRange((await uow.GetAllAsync<Account>()).OrderBy(x => x.Id));
                itemsToBackup.AddRange((await uow.GetAllAsync<AttributeDefinition>()).Where(x => x.Id > 0));
                itemsToBackup.AddRange(await uow.GetAllAsync<CategoryAttribute>());
                itemsToBackup.AddRange(await uow.GetAllAsync<CCardClosingDate>());
                itemsToBackup.AddRange(await uow.GetAllAsync<SmsTemplate>());
                itemsToBackup.AddRange((await uow.GetAllAsync<Category>()).Where(x => x.Id > 0));
            }

            await backupWriter.GenerateBackupAsync(itemsToBackup, backupPath, _backupVersion, _entityColumnsOrder);
        }

        private void AddKeylessEntities<T>(IEnumerable<T> entities)
        where T : Entity
        {
            List<T> materialized = entities as List<T> ?? entities.ToList();
            Logger.Info($"Imported {typeof(T).Name} {materialized.Count}");
            keyLessEntities.AddRange(materialized);
        }

        private BindableBase GetOrCreatePage(Type type)
        {
            switch (type.Name)
            {
                case nameof(AccountModel):
                    return GetOrCreatePage<AccountModel, AccountsVM>();
                case nameof(CurrencyModel):
                    return GetOrCreatePage<CurrencyModel, CurrenciesVM>();
                case nameof(ProjectModel):
                    return GetOrCreatePage<ProjectModel, ProjectsVM>();
                case nameof(LocationModel):
                    return GetOrCreatePage<LocationModel, LocationsVM>();
                case nameof(PayeeModel):
                    return GetOrCreatePage<PayeeModel, PayeesVM>();
                case nameof(BlotterModel):
                    return GetOrCreatePage<BlotterModel, BlotterVM>();
                case nameof(CategoryTreeModel):
                    return  GetOrCreatePage<CategoryTreeModel, CategoriesVM>();
                case nameof(ExchangeRateModel):
                    return _pages.GetOrAdd(type, _ => new ExchangeRatesVM(db, dialogWrapper, notifier));
                //case nameof(ReportsControlVM):
                //    return _pages.GetOrAdd(type, _ => new ReportsControlVM(db));
                case nameof(SettingsVM):
                    return _pages.GetOrAdd(type, _ => new SettingsVM(db, notifier));

                default: throw new NotSupportedException($"{type.FullName} not supported");
            }
        }

        private VMType GetOrCreatePage<TEntity, VMType>()
            where VMType : EntityBaseVM<TEntity>
            where TEntity : BaseModel, new()
        {
            var type = typeof(TEntity);
            return (VMType)_pages.GetOrAdd(type, _ => Activator.CreateInstance(typeof(VMType), db, dialogWrapper) as VMType);
        }

        private async Task NavigateToType(Type type)
        {
            CurrentPage = GetOrCreatePage(type);
            await RefreshCurrentPage();
        }

        private async Task OpenBackup_Click()
        {
            if (IsLoading) return;
            var backupPath = await dialogWrapper.OpenFileDialogAsync(Backup);
            if (!string.IsNullOrEmpty(backupPath))
            {
                Logger.Info($"Opened backup : {backupPath}");
                await OpenBackup(backupPath);
            }
        }

        private async Task RefreshCurrentPage()
        {
            if (CurrentPage is not IDataRefresh page)
            {
                return;
            }

            await page.RefreshDataCommand.ExecuteAsync();
        }

        private async Task SaveBackup_Click()
        {
            string defaultPath = string.IsNullOrEmpty(OpenBackupPath)
                ? BackupWriter.GenerateFileName()
                : Path.Combine(Path.GetDirectoryName(OpenBackupPath), BackupWriter.GenerateFileName());
            var backupPath = await dialogWrapper.SaveFileDialogAsync(Backup, defaultPath);
            if (!string.IsNullOrEmpty(backupPath))
            {
                await SaveBackup(backupPath);

                notifier.ShowMessage(string.Format(LocalizationService.Instance.saved_message, backupPath));
                Logger.Info($"Backup done. Saved {backupPath}");
            }
        }

        private async Task SaveBackupAsDb()
        {
            string fileName = Path.ChangeExtension(BackupWriter.GenerateFileName(), "db");
            string defaultPath = !string.IsNullOrEmpty(OpenBackupPath) ? Path.Combine(Path.GetDirectoryName(OpenBackupPath ?? string.Empty), fileName) : fileName;

            var backupPath = await dialogWrapper.SaveFileDialogAsync("db", defaultPath);
            if (!string.IsNullOrEmpty(backupPath))
            {
                await db.SaveAsFile(backupPath);

                notifier.ShowMessage(string.Format(LocalizationService.Instance.saved_message, backupPath));
                Logger.Info($"Backup done. Saved {backupPath}");
            }
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
