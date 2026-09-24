# Financisto.Desktop — Architecture Reference

## Purpose

The runnable Avalonia 12 app (`net10.0`, `WinExe`, self-contained single-file, `win-x64` by default; CI also publishes macOS through `MacBundle`). It holds the shell window, page and dialog VMs and views, DTOs, and services (settings, exchange rates, updates, dashboard totals). It references Common, DataAccess and Adapter.

There is **no DI container**: the `MainWindow` constructor creates every dependency itself.

## MVVM toolkits (mixed)

- Page VMs, `MainWindowVM` and DTOs use Prism `BindableBase` (`SetProperty` / `RaisePropertyChanged`). Page commands are Common's `AsyncCommand` / `IAsyncCommand`.
- `DialogBaseVM` uses a CommunityToolkit `ObservableObject`. `[RelayCommand]` generates `SaveCommand` and `CancelCommand`. Extra dialog commands use Prism `DelegateCommand` or Common's `AsyncCommand`.
- `AvaloniaUseCompiledBindingsByDefault=true`, so every view declares `x:DataType`. Views that need loose bindings (`$parent[...]`, DTOs reached through polymorphic collections) opt out with `x:CompileBindings="False"`: `AccountDialog`, `TransactionDialog`, `SubTransactionDialog`, `Controls/AmountControl`, and the `TreeViewItem` style in `CategoriesPageView`.
- A DataGrid column can't bind to the page VM (it isn't in the visual tree). `TagPageView` toggles its aliases column from code-behind (`Tag="aliases"` + `ITagBaseVM.HasAliases`).

## Startup

`Program.Main` → `App` → `new MainWindow()`. `App.axaml` registers `ViewLocator` in `Application.DataTemplates` and merges these resources: `avares://Financisto.Common/Assets/Generic.axaml` (converters, the `IActive` item template, `Icon*` DrawingImages), `Styles.axaml` and `DataGridStyles.axaml`.

```csharp
// Views/MainWindow.axaml.cs
ViewModel = new MainWindowVM(new DialogWrapper(), new FinancistoDatabaseFactory(), new EntityReader(),
                             new BackupWriter(), notificator /* ToastNotifierWrapper */, new UpdateService());
```

`MainWindow_Loaded` (code-behind) runs these steps:
1. `SettingsService.Current.Load()` reads the Cogwheel JSON settings from `Settings.dat` next to the exe, or from `FINANCISTO_SETTINGS_PATH` (see `StartOptions`). If the file is corrupt or missing, it falls back to defaults and shows a warning toast.
2. It applies the theme (`Application.RequestedThemeVariant`) and calls `LocalizationService.Instance.ApplyLanguage(...)`.
3. It auto-opens the newest `*.backup` in `General.DefaultBackupDir` through `ViewModel.OpenBackup(path)`. The folder falls back to `C:\Users\<user>\Dropbox\apps\Financisto Holo`.
4. If `CheckForUpdatesOnStart` is set, it calls `ViewModel.CheckForUpdatesAsync()`. Currently this check sits inside the "backup folder exists" branch.

## MainWindowVM

**File:** `src/Financisto.Desktop/ViewModels/MainWindowVM.cs` (Prism `BindableBase`)

```csharp
public class MainWindowVM : BindableBase
{
    MainWindowVM(IDialogWrapper, IFinancistoDatabaseFactory, IEntityReader, IBackupWriter,
                 IToastNotifierWrapper, UpdateService);

    BindableBase CurrentPage { get; private set; }
    bool IsLoading { get; }            // disables the menu and sidebar, shows the loading indicator
    bool IsPanelOpen { get; }          // SplitView pane
    string OpenBackupPath { get; }

    ObservableCollection<ListItemTemplate> ItemsTop, ItemsBottom;   // sidebar entries
    ListItemTemplate? SelectedItemTop, SelectedItemBottom;          // setting one navigates

    IAsyncCommand<Type> MenuNavigateCommand;
    IAsyncCommand OpenBackupCommand;
    IAsyncCommand SaveBackupCommand, SaveBackupAsDbCommand;         // CanExecute: a backup is loaded
    IAsyncCommand OpenPanelCommand;

    Task OpenBackup(string backupPath);   // also used by the startup auto-load
    Task SaveBackup(string backupPath);
    Task CheckForUpdatesAsync();
}
```

### Navigation

- The sidebar is two `ListBox`es in a `SplitView` pane (`Views/MainWindow.axaml`), bound to `ItemsTop` and `ItemsBottom`. Each entry is `ListItemTemplate(Type modelType, Func<string> label, string iconKey)`. The label is resolved again when the culture changes, and the icon is a `DrawingImage` resource looked up by key.
- Selecting an entry calls `NavigateInBackground(type)`, which runs `Task.Run(NavigateToType)`. `NavigateToType` calls `GetOrCreatePage(type)`, sets `CurrentPage` on the UI thread (`Dispatcher.UIThread.InvokeAsync`), then runs `RefreshDataCommand.ExecuteAsync()` if the page implements `IDataRefresh`. **Page refreshes run off the UI thread**, so marshal any UI-bound work through the Dispatcher.
- `_pages` is a `ConcurrentDictionary<Type, BindableBase>` keyed by the sidebar type. `GetOrCreatePage(Type)` switches on `type.Name`. `EntityBaseVM` pages are created with `Activator.CreateInstance(VMType, db, dialogWrapper)`. Dashboard, Settings and ExchangeRates call explicit constructors.
- `TransitioningContentControl Content="{Binding CurrentPage}"` renders the page. `ViewLocator` maps each VM type to its view through an **explicit `PageViews` dictionary**, not a naming convention. It matches any `BindableBase`.

| Sidebar key type | Page VM | View | Icon |
|---|---|---|---|
| `DashboardPageVM` | `DashboardPageVM` | `DashboardPageView` | IconGlance |
| `AccountModel` | `AccountsPageVM` | `AccountsPageView` | IconWallet |
| `CategoryTreeModel` | `CategoriesPageVM` | `CategoriesPageView` | IconFolderTree |
| `ProjectModel` | `ProjectsPageVM` | `TagPageView` (shared) | IconListCheck |
| `PayeeModel` | `PayeesPageVM` | `TagPageView` (shared) | IconAddressBook |
| `LocationModel` | `LocationsPageVM` | `LocationsPageView` | IconMap |
| `TagModel` | `TagsPageVM` | `TagPageView` (shared) | IconTags |
| `CurrencyModel` | `CurrenciesPageVM` | `CurrenciesPageView` | IconDollarSign |
| `ExchangeRateModel` | `ExchangeRatesPageVM` | `ExchangeRatesPageView` | IconArrowTrendUp |
| `BlotterModel` | `BlotterPageVM` | `BlotterPageView` | IconReceipt |
| `SettingsPageVM` (bottom list) | `SettingsPageVM` | `SettingsPageView` | IconGear |

### Adding a new page

1. Create the VM in `ViewModels/Pages/`. For a list page, derive from `EntityBaseVM<TModel>` with ctor `(IFinancistoDatabase, IDialogWrapper)`. For a tag-like page, derive from `TagBasePageVM<TModel>`. Otherwise use `BindableBase, IDataRefresh`.
2. Add a `case nameof(...)` to `MainWindowVM.GetOrCreatePage(Type)`.
3. Add a `ListItemTemplate` to `ItemsTop` or `ItemsBottom`, with a `LocalizationService.Instance.<key>` label and an `Icon*` resource key. Icons live in `Financisto.Common/Assets/Generic.axaml`.
4. Create `Views/XxxPageView.axaml` with `x:DataType="vm:XxxPageVM"` and register it in `ViewLocator.PageViews`.

### Backup open (`OpenBackup`)

1. `entityReader.ParseBackupFileAsync(path)` runs on a worker thread.
2. The entities are imported into a **new** database (`dbFactory.CreateDatabase()` + `ImportEntitiesAsync`). If that fails, the new database is disposed and the currently loaded one stays intact.
3. `db` is swapped, `_pages.Clear()` is called, and the old database is disposed. Pages are recreated lazily on the next navigation, bound to the new `db`.
4. `_backupVersion` and `_entityColumnsOrder` are stored (saving needs both), and CanExecute is raised on the save commands.
5. Keyless entities (`CCardClosingDate`, `CategoryAttribute`, `TransactionAttribute`) are kept in `keyLessEntities`. `ImportEntitiesAsync` inserts only `IIdentity` rows with `Id > 0`, so these rows never reach the DB and are written back as-is on save.
6. `DbManual.ResetAllDatabaseManuals()` and `DbManual.SetupAsync(db)` run, the app navigates to the Blotter and shows a toast. If `Settings.ExchangeRates.UpdateOnStart` is set, exchange rates are refreshed.

### Backup save

- `SaveBackup` collects `keyLessEntities` plus every repository: Budget, TransactionAttribute, CurrencyExchangeRate, Currency, Location (Id>0), Payee, Project (Id>0), Tag (Id>0), Transaction, Account (ordered by Id), AttributeDefinition (Id>0), CategoryAttribute, CCardClosingDate, SmsTemplate, Category (Id>0). It then calls `backupWriter.GenerateBackupAsync(items, path, _backupVersion, _entityColumnsOrder)`. **A new entity type must also be added here**, or it is silently dropped from saved backups.
- The default file name is `BackupWriter.GenerateFileName()`, placed in the opened backup's folder.
- `SaveBackupAsDb` calls `db.SaveAsFile(path)` (SQLite `VACUUM main INTO`).

## Page VMs

All in `src/Financisto.Desktop/ViewModels/Pages/`.

### EntityBaseVM\<T\> (abstract)

```csharp
public abstract class EntityBaseVM<T> : BaseViewModel<T>   // Common; gives db, Entities, RefreshDataCommand
    where T : BaseModel, new()
{
    protected EntityBaseVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper);
    protected readonly IDialogWrapper dialogWrapper;
    public T SelectedValue { get; set; }

    public IAsyncCommand AddCommand;
    public IAsyncCommand EditCommand;      // CanExecute: SelectedValue != null
    public IAsyncCommand DeleteCommand;    // CanExecute: SelectedValue != null

    protected abstract Task OnAdd();
    protected abstract Task OnEdit(T item);
    protected abstract Task OnDelete(T item);
    protected virtual void OnSelectedValueChanged();   // raises Edit/Delete CanExecuteChanged
    // from BaseViewModel<T>: protected abstract Task RefreshData();
}
```

### TagBasePageVM\<TEntity\> + ITagBaseVM

`TagBasePageVM<TEntity> : EntityBaseVM<TEntity>, ITagBaseVM` where `TEntity : TagBaseModel`. The non-generic `ITagBaseVM` (`PageTitle`, `Entities`, `HasAliases`, `SelectedValue`, Add/Edit/Delete commands) lets the single shared `TagPageView` (`x:DataType="vm:ITagBaseVM"`) serve Payees, Projects and Tags. Derived pages override `TitleKey` (a localization key) and optionally `HasAliases`. `OpenTagDialogAsync<T>(id)` (where `T : TagBase`) does GetOrCreate → `TagDialogVM(new TagDto(entity))` → copy `IsActive`/`Title` → `ApplyAliases` → `InsertOrUpdateAsync` → `RefreshData`. The grid shows aliases via `TagBaseModel.AliasesText` (comma-joined; only `PayeeModel`/`LocationModel` override it).

### Page summary

| VM | Base | Dialog (view / VM / DTO) | Notes |
|---|---|---|---|
| `DashboardPageVM` | `BindableBase, IDataRefresh` | — | LiveCharts2 `ISeries[]` (structure pie, saldo bars) + accounts-total list via `Services/AccountsTotalService` (port of Android `getAccountsTotal`). |
| `AccountsPageVM` | `EntityBaseVM<AccountModel>` | `AccountDialog` / `AccountDialogVM` / `AccountDto` | Delete = **soft** (`IsActive=false`). A new account with an opening amount inserts a transaction (`Id = 0`) and rebuilds the balance. |
| `BlotterPageVM` | `EntityBaseVM<BlotterModel>` | `TransactionDialog` / `TransactionDialogVM` / `TransactionDto`; `TransferDialog` / `TransferDialogVM` / `TransferDto` | Extra: `AddTransferCommand`, `DuplicateCommand`, `ClearFiltersCommand`, `SelectionChangedCommand` (+`SelectionSummary`). `AddTemplateCommand`/`InfoCommand` are disabled stubs. |
| `CategoriesPageVM` | `EntityBaseVM<CategoryTreeModel>` | `CategoryDialog` / `CategoryDialogVM` / `CategoryDto` | Nested-set tree (`Left`/`Right`). MoveTop/Up/Down/Bottom, SortByTitle rewrite the tree. Restores expand/select state. Delete not implemented. |
| `CurrenciesPageVM` | `EntityBaseVM<CurrencyModel>` | `NewCurrencyDialog` / `NewCurrencyDialogVM` (template picker from `DbManual.AllCurrencies`), then `CurrencyDialog` / `CurrencyDialogVM` / `CurrencyDto` | Delete is blocked while an account or transaction uses the currency. |
| `LocationsPageVM` | `EntityBaseVM<LocationModel>` | `LocationDialog` / `LocationDialogVM : TagDialogVM` / `LocationDto : TagDto` | Own view (address column). Delete not implemented. |
| `PayeesPageVM` | `TagBasePageVM<PayeeModel>` | `TagDialog` / `TagDialogVM` / `TagDto` | `HasAliases = true`. Delete not implemented. |
| `ProjectsPageVM` | `TagBasePageVM<ProjectModel>` | same | Delete not implemented. |
| `TagsPageVM` | `TagBasePageVM<TagModel>` | same | **Bug:** `OnAdd` calls `OpenTagDialogAsync<Project>(0)` (creates a Project). Delete not implemented. |
| `ExchangeRatesPageVM` | `EntityBaseVM<ExchangeRateModel>` | none | Read-only list with From/To currency pickers. `RefreshExchangeRatesCommand` downloads rates via `Services/ExchangeRatesService` (Monobank / OpenExchangeRates / FreeCurrencyRates, chosen in settings). Add/Edit/Delete throw `NotImplementedException`. |
| `SettingsPageVM` | `BindableBase, IDataRefresh` | — | Edits a clone of `SettingsService.Current.Settings` (`SettingsDto`: General + ExchangeRates). Save, browse backup dir, check for updates (`UpdateService`, Onova + GitHub releases). The OpenExchangeRates app id is DPAPI-encrypted (`Helpers/SettingsProtection`). |

### BlotterPageVM details

- **Filters** (bound from `BlotterPageView` to Common's filter controls): `PeriodType`, `From`/`To` (`DateTime?`), `Account`, `Category`, `Payee`, `Project`, `Location` (models from `DbManual`; the "all" entry has `Id == null`), and `Tags` (`ObservableCollection<TagModel>`, OR-matched with a substring `Contains`).
- **RefreshData** builds an `Expression<Func<BlotterTransactions,bool>>` with `ExpressionExtensions.And/Or`, queries the `v_blotter` view through `FindManyAndProjectAsync` and projects into `BlotterModel` (currencies and projects resolved from `DbManual.CurrencyIds`/`ProjectIds`), ordered by date descending.
- **Edit/Duplicate** dispatch on `BlotterModel.Type == "Transfer"`. Duplicate sets `Id = 0` on the parent and every split part.
- **Save transaction:** `MapperHelper.MapTransaction` → split parts get `Parent`, `FromAccountId` and `ParentAccountId` from the parent. Sub-transfers use `MapperHelper.MapTransfer`; they are **dropped when the parent has a foreign original currency**. Then `InsertOrUpdateAsync(all)`, delete removed parts, `RebuildAccountBalanceAsync` for the from-account and every to-account, and `RefreshData`.
- **Delete** removes the row and its split parts (`Id == id || ParentId == id`), then rebuilds the affected balances.

## Dialog system

### IDialogWrapper → DialogWrapper (`Helpers/`)

```csharp
public interface IDialogWrapper
{
    Task<object?> ShowDialogAsync<T>(DialogBaseVM context, double height, double width, string title = null)
        where T : UserControl, new();
    Task<string> OpenFileDialogAsync(string fileExtension);                    // "" when cancelled
    Task<string> SaveFileDialogAsync(string fileExtension, string defaultPath = "");
    Task<string> OpenFolderDialogAsync(string defaultPath = "");
    Task<bool>   ShowMessageBoxAsync(string text, string caption, bool yesNoButtons = false);  // Views/Dialogs/MessageBoxWindow
}
```

Everything is async and uses Avalonia `StorageProvider` pickers. The owner is `desktop.MainWindow`; without an owner, the calls return `null`, `""` or `true`.

`IToastNotifierWrapper` → `ToastNotifierWrapper` (`ShowMessage`, `ShowWarning`) is built on Message.Avalonia, with a `<msg:MessageHost/>` in `MainWindow.axaml`.

### DialogBaseVM

```csharp
public abstract partial class DialogBaseVM : ObservableObject     // CommunityToolkit
{
    public event EventHandler RequestCancel;
    public event EventHandler RequestSave;          // sender = the object returned by OnRequestSave()
    public abstract object OnRequestSave();
    protected virtual bool CanSaveCommandExecute() => true;
    // [RelayCommand] Cancel → CancelCommand
    // [RelayCommand(CanExecute = nameof(CanSaveCommandExecute))] Save → SaveCommand
}
```

**Flow:**
1. The page VM builds a DTO and a dialog VM, then calls `await dialogWrapper.ShowDialogAsync<XxxDialog>(vm, height, width, title)`.
2. `DialogWrapper` creates a non-resizable `Window { Content = new T { DataContext = vm } }` and closes it on `RequestSave` (the result is the DTO) or `RequestCancel` (the result is `null`).
3. The page VM maps the DTO back onto the entity (transactions use `MapperHelper`), calls `db.InsertOrUpdateAsync(...)`, rebuilds balances where needed, resets the `DbManual` caches it touched and calls `RefreshData()`.

**Save guard:** `SaveCommand`'s CanExecute is **not** re-evaluated automatically. A VM whose guard depends on DTO properties subscribes to `PropertyChanged` and calls `SaveCommand.NotifyCanExecuteChanged()` for a tracked list of properties (see `TrackingProperies` in `SubTransactionDialogVM` and `TransferDialogVM`).

### Dialog VM summary

All in `src/Financisto.Desktop/ViewModels/Dialogs/`. Views are in `Views/Dialogs/` (`Financisto.Desktop.Views.Dialogs.*`, `x:DataType` = the dialog VM).

| Dialog VM | View | DTO | Save guard |
|---|---|---|---|
| `AccountDialogVM(AccountDto, bool isNew)` | `AccountDialog` | `AccountDto` | Title not blank && `CurrencyId > 0` |
| `CategoryDialogVM` | `CategoryDialog` | `CategoryDto` | — |
| `SubTransactionDialogVM(TransactionDto)` | `SubTransactionDialog` | `TransactionDto` | `!IsSplitCategory \|\| UnsplitAmount == 0` |
| `TransactionDialogVM(TransactionDto, IDialogWrapper) : SubTransactionDialogVM` | `TransactionDialog` | `TransactionDto` | `FromAccount != null && FromAmount != 0 && base` |
| `TransferDialogVM(TransferDto)` | `TransferDialog` | `TransferDto` | From and To set && `FromAccountId != ToAccountId` |
| `TagDialogVM(TagDto)` | `TagDialog` | `TagDto` | — (`ShowAliases` = `Entity.SupportsAliases`) |
| `LocationDialogVM : TagDialogVM` | `LocationDialog` | `LocationDto` | — |
| `CurrencyDialogVM` | `CurrencyDialog` | `CurrencyDto` | — |
| `NewCurrencyDialogVM` | `NewCurrencyDialog` | returns `CurrencyTemplateItem` | — |

`TransactionDialogVM` opens nested dialogs for split parts: `AddSubTransactionCommand` / `EditSubTransactionCommand` → `SubTransactionDialog`, and `AddSubTransferCommand` → `TransferDialog` with `IsSubTransaction = true`. It edits working copies and copies them back on save.

## DTOs

All in `src/Financisto.Desktop/Data/` (namespace `Financisto.Desktop.Data`), Prism `BindableBase`. Each has a constructor from its entity. Writing back is manual: done in the page VM, or in `Helpers/MapperHelper` for transactions and transfers.

- **AccountDto:** Id, Title, Type (string, `AccountType` name), CurrencyId, CardIssuer, Issuer, Number, LimitAmount, SortOrder, IsActive, IsIncludeIntoTotals, Note, ClosingDay, PaymentDay, OpeningAmount.
- **BaseTransactionDto** (abstract): `Date` + `Time` → computed `DateTime`, Id, Note, `IsSubTransaction`, virtual `RealFromAmount`, `SubTransactionTitle`, `IsAmountNegative`, `Rate`.
- **TransactionDto : BaseTransactionDto:** FromAccountId/FromAccount (`AccountFilterModel`), CategoryId/Category, PayeeId, ProjectId, LocationId, OriginalCurrencyId/OriginalCurrency, OriginalFromAmount, FromAmount, `SubTransactions` (`ObservableCollection<BaseTransactionDto>`, which holds `TransactionDto` and `TransferDto`), `SelectedTags` (`ObservableCollection<TagModel>`). Computed: `IsSplitCategory` (CategoryId == -1), `SplitAmount`, `UnsplitAmount`, `IsOriginalFromAmountVisible`, `RateString`. `TagsDelimiter = "\\n"`.
- **TransferDto : BaseTransactionDto:** FromAccountId/FromAccount, ToAccountId/ToAccount, FromAmount, ToAmount, currencies. Computed: `IsToAmountVisible` (different currencies), `RateString`. `IsAmountNegative` is always `true`, and `RealFromAmount = -|FromAmount|`.
- **CategoryDto:** Id, Title, ParentId, IsIncome, Left, Right.
- **TagDto:** Title, IsActive, and aliases support: `SupportsAliases` (the entity is `IHasAliases`), `Aliases` (one per line), `ApplyAliases(entity)`. **LocationDto : TagDto** adds Address.
- **CurrencyDto:** Id, Title, Name, Symbol, IsDefault, UpdateExchangeRate, Decimals, DecimalSeparator, GroupSeparator, `SymbolFormat` (enum), NumberFormat.
- **SettingsDto** (`SettingsDTO.cs`) holds `SettingsGeneralDto` (CheckForUpdatesOnStart, DefaultBackupDir, Language, CurrentAppTheme, ThemeVariant) and `SettingsExchangeRates` (Provider, OpenExchangeRatesProviderAppId, UpdateOnStart).

`MapperHelper.MapTransaction(dto, tr)` writes FromAmount/OriginalFromAmount with the sign from `IsAmountNegative`, nulls navigation properties, clears `ProjectId` for a split parent, and joins `SelectedTags` with `TagsDelimiter`. `MapperHelper.MapTransfer(dto, tr)` sets `FromAmount = -|x|`, `ToAmount` (own amount when the currencies differ), `OriginalCurrencyId`/`OriginalFromAmount` when the currencies differ, and `CategoryId = 0`.

## Services (`src/Financisto.Desktop/Services/`)

- `SettingsService.Current` is a Cogwheel `SettingsBase` with a source-generated JSON context. It exposes `Settings` (`SettingsDto`), `Load()` and `Save()`.
- `ExchangeRatesService` loads rates from Monobank, OpenExchangeRates (encrypted app id) or FreeCurrencyRates as `CurrencyExchangeRate` entities.
- `UpdateService` wraps Onova `UpdateManager` + `GithubPackageResolver("vov4uk","Financisto.Desktop", "Financisto.Desktop.<rid>.zip")`.
- `AccountsTotalService` + `LatestExchangeRates` compute dashboard totals per currency and in the home currency.

## Leftovers (unused scaffold)

`Models/Categoria.cs`, `Models/Transacao.cs`, `Enum/TipoTransacao.cs`, `Enum/AppTheme.cs` (the settings use Common's `AppThemeType`) and `ViewModels/Charts/*` are not referenced by the running app.
