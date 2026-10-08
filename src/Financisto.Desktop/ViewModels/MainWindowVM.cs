using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Financisto.Adapter;
using Financisto.BankHelpers;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.Converters;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.DataAccess.Utils;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Helpers.BankHelper;
using Financisto.Desktop.Services;
using Financisto.Desktop.ViewModels.Pages;
using Financisto.Desktop.Wizards;
using Financisto.Desktop.Wizards.MonoWizard.ViewModel;
using Financisto.Reports;
using Prism.Mvvm;
using IAsyncCommand = Financisto.Common.IAsyncCommand;

namespace Financisto.Desktop.ViewModels
{
    public class MainWindowVM : BindableBase
    {
        private const string Backup = "backup";
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ConcurrentDictionary<Type, BindableBase> _pages = new ConcurrentDictionary<Type, BindableBase>();
        private readonly IBackupWriter backupWriter;
        private readonly IFinancistoDatabaseFactory dbFactory;
        private readonly IDialogWrapper dialogWrapper;
        private readonly IEntityReader entityReader;
        private readonly List<Entity> keyLessEntities = new();
        private readonly IToastNotifierWrapper notifier;
        private readonly UpdateService updateService;
        private BackupVersion _backupVersion;
        private Dictionary<string, List<string>> _entityColumnsOrder;
        private IAsyncCommand<IBankHelper> _importCommand;
        private IAsyncCommand<Type> _menuNavigateCommand;
        private IAsyncCommand _openBackupCommand;
        private IAsyncCommand _openPanelCommand;
        private IAsyncCommand _saveBackupAsDbCommand;
        private IAsyncCommand _saveBackupCommand;
        private BindableBase currentPage;
        private IFinancistoDatabase db;
        private bool isLoading;
        private bool isPanelOpen = true;
        private string openBackupPath;
        private ListItemTemplate? selectedItemBottom;
        private ListItemTemplate? selectedItemTop;
        public MainWindowVM(IDialogWrapper dialogWrapper,
            IFinancistoDatabaseFactory dbFactory,
            IEntityReader entityReader,
            IBackupWriter backupWriter,
            IToastNotifierWrapper notifier,
            IBankHelperProvider bankHelpers,
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

        public bool IsLoading
        {
            get => isLoading;
            private set => SetProperty(ref isLoading, value);
        }

        public bool IsPanelOpen
        {
            get => isPanelOpen;
            private set => SetProperty(ref isPanelOpen, value);
        }

        public ObservableCollection<ListItemTemplate> ItemsBottom { get; } = new()
        {
            new(typeof(SettingsPageVM), () => LocalizationService.Instance.settings, "IconGear"),
        };

        public ObservableCollection<ListItemTemplate> ItemsTop { get; } = new()
        {
            new(typeof(DashboardPageVM), () => LocalizationService.Instance.dashboard, "IconGlance"),
            new(typeof(AccountModel), () => LocalizationService.Instance.accounts, "IconWallet"),
            new(typeof(CurrencyModel), () => LocalizationService.Instance.currencies, "IconDollarSign"),
            new(typeof(CategoryTreeModel), () => LocalizationService.Instance.categories, "IconFolderTree"),
            new(typeof(TagModel), () => LocalizationService.Instance.tags, "IconTags"),
            new(typeof(SmsTemplateModel), () => LocalizationService.Instance.sms_templates, "IconNotification"),
            new(typeof(ProjectModel), () => LocalizationService.Instance.projects, "IconListCheck"),
            new(typeof(PayeeModel), () => LocalizationService.Instance.payees, "IconAddressBook"),
            new(typeof(LocationModel), () => LocalizationService.Instance.locations, "IconMap"),
            new(typeof(ExchangeRateModel), () => LocalizationService.Instance.exchange_rates, "IconArrowTrendUp"),
            new(typeof(BlotterModel), () => LocalizationService.Instance.blotter, "IconReceipt"),
            new(typeof(ReportsControlVM), () => LocalizationService.Instance.reports, "IconChartBar"),
        };

        public IAsyncCommand<IBankHelper> ImportCommand => _importCommand ??= new AsyncCommand<IBankHelper>(OpenImportWizardAsync, _ => IsBackupLoaded);

        public IAsyncCommand<Type> MenuNavigateCommand => _menuNavigateCommand ??= new AsyncCommand<Type>(NavigateToType);

        public IAsyncCommand OpenBackupCommand => _openBackupCommand ??= new AsyncCommand(OpenBackup_Click);

        public string OpenBackupPath
        {
            get => openBackupPath;
            private set => SetProperty(ref openBackupPath, value);
        }
        public IAsyncCommand OpenPanelCommand => _openPanelCommand ??= new AsyncCommand(OpenPanelAsync);

        public IAsyncCommand SaveBackupAsDbCommand => _saveBackupAsDbCommand ??= new AsyncCommand(SaveBackupAsDb, () => IsBackupLoaded);

        public IAsyncCommand SaveBackupCommand => _saveBackupCommand ??= new AsyncCommand(SaveBackup_Click, () => IsBackupLoaded);

        private bool IsBackupLoaded => _backupVersion != null && _entityColumnsOrder != null;

        public ListItemTemplate? SelectedItemBottom
        {
            get => selectedItemBottom;
            set
            {
                SetProperty(ref selectedItemBottom, value);
                if (value != null)
                {
                    SelectedItemTop = null;

                    NavigateInBackground(value.ModelType);
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

                    NavigateInBackground(value.ModelType);
                }
            }
        }
        public async Task CheckForUpdatesAsync()
        {
            var settingsPageVM = _pages.GetOrAdd(typeof(SettingsPageVM), _ => new SettingsPageVM(db, dialogWrapper, notifier, updateService)) as SettingsPageVM;
            await settingsPageVM?.CheckForUpdateCommand?.ExecuteAsync()!;
        }

        public async Task OpenBackup(string backupPath)
        {
            try
            {
                IsLoading = true;
                Stopwatch stopwatch = Stopwatch.StartNew();
                var (entities, backupVersion, columnsOrder) = await Task.Run(() => entityReader.ParseBackupFileAsync(backupPath));
                entities = entities as IReadOnlyCollection<Entity> ?? entities.ToList();

                // Import into a fresh database first; the currently loaded one stays intact if this backup fails to load.
                var newDb = await Task.Run(async () =>
                {
                    var created = dbFactory.CreateDatabase();
                    try
                    {
                        await created.ImportEntitiesAsync(entities);
                        return created;
                    }
                    catch
                    {
                        created.Dispose();
                        throw;
                    }
                });

                var previousDb = db;
                db = newDb;
                _pages.Clear();
                previousDb?.Dispose();

                OpenBackupPath = backupPath;
                _backupVersion = backupVersion;
                _entityColumnsOrder = columnsOrder;
                SaveBackupCommand.RaiseCanExecuteChanged();
                SaveBackupAsDbCommand.RaiseCanExecuteChanged();
                ImportCommand.RaiseCanExecuteChanged();

                keyLessEntities.Clear();

                AddKeylessEntities(entities.OfType<CCardClosingDate>());
                AddKeylessEntities(entities.OfType<CategoryAttribute>());
                AddKeylessEntities(entities.OfType<TransactionAttribute>());

                DbManual.ResetAllDatabaseManuals();
                await Task.Run(() => DbManual.SetupAsync(db));

                stopwatch.Stop();
                int entitiesCount = entities?.Count() ?? 0;
                Logger.Info($"Backup loaded in {stopwatch.ElapsedMilliseconds} ms. Backup version : {_backupVersion}. Entities count : {entitiesCount}");

                await NavigateToType(typeof(BlotterModel));

                IsLoading = false;

                notifier?.ShowMessage(string.Format(LocalizationService.Instance.entities_loaded, entitiesCount));

                if (SettingsService.Current.Settings?.ExchangeRates.UpdateOnStart == true)
                {
                    var exchangeRatesVM = _pages.GetOrAdd(typeof(ExchangeRateModel), _ => new ExchangeRatesPageVM(db, dialogWrapper, notifier!)) as ExchangeRatesPageVM;
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
            if (!IsBackupLoaded)
            {
                throw new InvalidOperationException("Open a backup before saving.");
            }

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
                itemsToBackup.AddRange((await uow.GetAllAsync<Tag>()).Where(x => x.Id > 0));
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
                    return GetOrCreatePage<AccountModel, AccountsPageVM>();
                case nameof(CurrencyModel):
                    return GetOrCreatePage<CurrencyModel, CurrenciesPageVM>();
                case nameof(ProjectModel):
                    return GetOrCreatePage<ProjectModel, ProjectsPageVM>();
                case nameof(LocationModel):
                    return GetOrCreatePage<LocationModel, LocationsPageVM>();
                case nameof(PayeeModel):
                    return GetOrCreatePage<PayeeModel, PayeesPageVM>();
                case nameof(TagModel):
                    return GetOrCreatePage<TagModel, TagsPageVM>();
                case nameof(BlotterModel):
                    return GetOrCreatePage<BlotterModel, BlotterPageVM>();
                case nameof(CategoryTreeModel):
                    return GetOrCreatePage<CategoryTreeModel, CategoriesPageVM>();
                case nameof(SmsTemplateModel):
                    return GetOrCreatePage<SmsTemplateModel, SmsTemplatesPageVM>();
                case nameof(ExchangeRateModel):
                    return _pages.GetOrAdd(type, _ => new ExchangeRatesPageVM(db, dialogWrapper, notifier));
                case nameof(ReportsControlVM):
                    return _pages.GetOrAdd(type, _ => new ReportsControlVM(db, new ReportDialogService(dialogWrapper)));
                case nameof(SettingsPageVM):
                    return _pages.GetOrAdd(type, _ => new SettingsPageVM(db, dialogWrapper, notifier, updateService));
                case nameof(DashboardPageVM):
                    return _pages.GetOrAdd(type, _ => new DashboardPageVM(db));

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

        private void NavigateInBackground(Type type)
        {
            // Page refresh stays off the UI thread so large backups don't freeze the window; failures are logged, not lost.
            Task.Run(() => NavigateToType(type)).ContinueWith(
                t => Logger.Error(t.Exception, $"Navigation to {type.FullName} failed"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        private async Task NavigateToType(Type type)
        {
            var page = GetOrCreatePage(type);
            await Dispatcher.UIThread.InvokeAsync(() => CurrentPage = page);
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

        private async Task OpenImportWizardAsync(IBankHelper importHelper)
        {
            var fileExtension = importHelper.ReportType.GetFileExtension();
            var fileName = await dialogWrapper.OpenFileDialogAsync(fileExtension);
            Logger.Info($"{fileExtension} fileName -> {fileName}");
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            List<BankTransaction> sourceData;
            try
            {
                sourceData = await Task.Run(() => importHelper.ParseReport(fileName).ToList());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"{importHelper.BankTitle} statement {fileName} could not be parsed");
                notifier.ShowWarning(string.Format(LocalizationService.Instance.import_failed, importHelper.BankTitle));
                return;
            }

            var lastTransactions = await GetLastTransactionsAsync();

            var vm = new MonoWizardVM(importHelper.BankTitle, sourceData, lastTransactions, dialogWrapper);

            var output = await dialogWrapper.ShowWizardAsync(vm);

            // The wizard only adds new rules to DbManual.Rules; store them and reload to get their ids.
            if (DbManual.Rules.Any(r => !(r.Id > 0)))
            {
                await new RulesRepository(db).SaveAsync(DbManual.Rules);
                DbManual.ResetManuals(nameof(DbManual.Rules));
                await DbManual.SetupAsync(db);
            }

            if (output is List<Transaction> outputTransactions)
            {
                var (imported, duplicatesCount) = await Task.Run(() => ImportTransactionsAsync(outputTransactions));
                await RefreshCurrentPage();

                var message = duplicatesCount > 0
                    ? string.Format(LocalizationService.Instance.import_result_with_duplicates, imported, duplicatesCount)
                    : string.Format(LocalizationService.Instance.import_result, imported);

                notifier.ShowMessage(message);

                Logger.Info($"Imported {imported} transactions. Found duplicates : {duplicatesCount}");
            }
        }

        /// <summary>Account id → its latest blotter row (null when the account has none), for the wizard's "last transaction" hint.</summary>
        private async Task<Dictionary<int, BlotterModel>> GetLastTransactionsAsync()
        {
            var accounts = DbManual.Account.Where(x => x.Id.HasValue).ToList();
            var lastIds = accounts.Select(x => x.LastTransactionId).Where(id => id > 0).Distinct().ToList();

            // A split part isn't a blotter row, so an account whose latest transaction is one shows the split instead.
            Dictionary<int, int> splitParents;
            using (var uow = db.CreateUnitOfWork())
            {
                splitParents = (await uow.GetRepository<Transaction>().FindManyAsync(x => lastIds.Contains(x.Id) && x.ParentId > 0))
                    .ToDictionary(x => x.Id, x => x.ParentId);
            }

            int BlotterRowId(int transactionId) => splitParents.GetValueOrDefault(transactionId, transactionId);

            var rowIds = lastIds.Select(BlotterRowId).Distinct().ToList();
            var lastRows = (await BlotterPageVM.QueryAsync(db, x => rowIds.Contains(x.Id))).ToDictionary(x => x.Id);

            return accounts.ToDictionary(acc => acc.Id!.Value, acc => lastRows.GetValueOrDefault(BlotterRowId(acc.LastTransactionId))!);
        }

        /// <summary>Adds the wizard output, skipping rows that already exist (same account, time and amount).</summary>
        private async Task<(int Imported, int Duplicates)> ImportTransactionsAsync(List<Transaction> outputTransactions)
        {
            var times = outputTransactions.Select(x => x.DateTime).Distinct().ToArray();
            List<Transaction> accTransactions;
            using (var uow = db.CreateUnitOfWork())
            {
                accTransactions = await uow.GetRepository<Transaction>().FindManyAsync(predicate: x => times.Contains(x.DateTime));
            }

            var accTransactionKeys = accTransactions
                .Select(x => (x.FromAccountId, x.DateTime, x.FromAmount))
                .ToHashSet();

            List<Transaction> toImport = outputTransactions
                .Where(item => !accTransactionKeys.Contains((item.FromAccountId, item.DateTime, item.FromAmount)))
                .ToList();

            await db.AddTransactionsAsync(toImport);
            await RefreshAffectedAccounts(toImport);

            return (toImport.Count, outputTransactions.Count - toImport.Count);
        }

        private Task OpenPanelAsync()
        {
            IsPanelOpen = !IsPanelOpen;
            return Task.CompletedTask;
        }

        private async Task RefreshAffectedAccounts(List<Transaction> transactions)
        {
            var accountIds = transactions
                .Where(x => x.ToAccountId > 0)
                .Select(x => x.ToAccountId).Union(
                transactions
                .Where(x => x.FromAccountId > 0)
                .Select(x => x.FromAccountId))
                .Distinct();

            foreach (var accId in accountIds)
            {
                await db.RebuildAccountBalanceAsync(accId);
            }

            DbManual.ResetManuals(nameof(DbManual.Account));
            await DbManual.SetupAsync(db);
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
    }
}
