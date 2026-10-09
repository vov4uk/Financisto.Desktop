# Financisto.Desktop — Architecture Reference

## Purpose

The runnable Avalonia 12 app (`net10.0`, `WinExe`, self-contained single-file, `win-x64` by default; CI also publishes macOS through `MacBundle`). It holds the shell window, page and dialog VMs and views, DTOs, services (settings, exchange rates, updates, dashboard totals), and the bank statement import (wizards, rules, and the loader for the bank helper plugins). It references Common, DataAccess, Adapter, Reports (the Reports page is its own project, see `reports_architecture.md`) and `Financisto.BankHelpers.Abstractions`. The statement parsers are **not** in this project: each bank is a plugin DLL in `<app>/plugins` (see `bankhelpers_architecture.md`).

There is **no DI container**: the `MainWindow` constructor creates every dependency itself.

## MVVM toolkits (mixed)

- Page VMs, `MainWindowVM` and DTOs use Prism `BindableBase` (`SetProperty` / `RaisePropertyChanged`). Page commands are Common's `AsyncCommand` / `IAsyncCommand`.
- `DialogBaseVM` uses a CommunityToolkit `ObservableObject`. `[RelayCommand]` generates `SaveCommand` and `CancelCommand`. Extra dialog commands use Prism `DelegateCommand` or Common's `AsyncCommand`.
- `AvaloniaUseCompiledBindingsByDefault=true`, so every view declares `x:DataType`. Views that need loose bindings (`$parent[...]`, DTOs reached through polymorphic collections) opt out with `x:CompileBindings="False"`: `AccountDialog`, `TransactionDialog`, `SubTransactionDialog`, `Controls/AmountControl`, the import wizard's `MonoWizard/Page3`, the recipes wizard's `RecipesWizard/Page2`, and the `TreeViewItem` style in `CategoriesPageView`.
- A `DataGridTextColumn` binding with a one-way converter (no `ConvertBack`) needs `Mode=OneWay`, or every cell logs a ConvertBack binding warning.
- A DataGrid column can't bind to the page VM (it isn't in the visual tree). `TagPageView` toggles its aliases column from code-behind (`Tag="aliases"` + `ITagBaseVM.HasAliases`).

## Startup

`Program.Main` → `App` → `new MainWindow()`. `App.axaml` registers `ViewLocator` in `Application.DataTemplates` and merges these resources: `avares://Financisto.Common/Assets/Generic.axaml` (converters, the `IActive` item template, `Icon*` DrawingImages), `Styles.axaml` and `DataGridStyles.axaml`.

```csharp
// Views/MainWindow.axaml.cs
ViewModel = new MainWindowVM(new DialogWrapper(), new FinancistoDatabaseFactory(), new EntityReader(),
                             new BackupWriter(), notificator /* ToastNotifierWrapper */,
                             new PluginBankHelperProvider(StartOptions.Current.PluginsPath), new UpdateService());
// then PopulateImportMenu() fills the Import menu from ViewModel.ImportGroups
```

`StartOptions.Current.PluginsPath` is `<exe dir>/plugins`, or the `FINANCISTO_PLUGINS_PATH` environment variable (the tests and the headless harness point it elsewhere). The plugins are scanned in the `MainWindow` constructor (about 50 ms for 8 plugins, plus a one-off JIT warm-up).

Import rules are stored in the backup's `sms_template` table (see [Bank statement import](#bank-statement-import-wizards)).

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
                 IToastNotifierWrapper, IBankHelperProvider, UpdateService);

    BindableBase CurrentPage { get; private set; }
    bool IsLoading { get; }            // disables the menu and sidebar, shows the loading indicator
    bool IsPanelOpen { get; }          // SplitView pane
    string OpenBackupPath { get; }

    ObservableCollection<ListItemTemplate> ItemsTop, ItemsBottom;   // sidebar entries
    ListItemTemplate? SelectedItemTop, SelectedItemBottom;          // setting one navigates

    IAsyncCommand<Type> MenuNavigateCommand;
    IAsyncCommand OpenBackupCommand;
    IAsyncCommand SaveBackupCommand, SaveBackupAsDbCommand;         // CanExecute: a backup is loaded
    IAsyncCommand<IBankHelper> ImportCommand;                       // Import menu; parameter = the helper; CanExecute: a backup is loaded
    IReadOnlyList<IReadOnlyList<BankImportItem>> ImportGroups;      // the Import menu: one group per ReportType (enum order), items sorted by title
    IAsyncCommand OpenPanelCommand;

    Task OpenBackup(string backupPath);   // also used by the startup auto-load
    Task SaveBackup(string backupPath);
    Task CheckForUpdatesAsync();
}
```

### Navigation

- The sidebar is two `ListBox`es in a `SplitView` pane (`Views/MainWindow.axaml`), bound to `ItemsTop` and `ItemsBottom`. Each entry is `ListItemTemplate(Type modelType, Func<string> label, string iconKey)`. The label is resolved again when the culture changes, and the icon is a `DrawingImage` resource looked up by key.
- Selecting an entry calls `NavigateInBackground(type)`, which runs `Task.Run(NavigateToType)`. `NavigateToType` calls `GetOrCreatePage(type)`, sets `CurrentPage` on the UI thread (`Dispatcher.UIThread.InvokeAsync`), then runs `RefreshDataCommand.ExecuteAsync()` if the page implements `IDataRefresh`. **Page refreshes run off the UI thread**, so marshal any UI-bound work through the Dispatcher.
- `_pages` is a `ConcurrentDictionary<Type, BindableBase>` keyed by the sidebar type. `GetOrCreatePage(Type)` switches on `type.Name`. `EntityBaseVM` pages are created with `Activator.CreateInstance(VMType, db, dialogWrapper)`. Dashboard, Settings, ExchangeRates and Accounts call explicit constructors; the accounts page gets `ShowAccountTransactions`, which `MainWindowVM.ShowAccountTransactionsAsync` implements: `BlotterPageVM.ShowAccount(account)` (resets every blotter filter, then filters to that account), then selecting the Blotter sidebar entry navigates and refreshes.
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
| `RuleModel` | `RulesPageVM` | `RulesPageView` | IconBoltLightning |
| `ReportsControlVM` | `ReportsControlVM` (Financisto.Reports) | `ReportsControl` (Financisto.Reports) | IconChartBar |
| `SettingsPageVM` (bottom list) | `SettingsPageVM` | `SettingsPageView` | IconGear |

### Adding a new page

1. Create the VM in `ViewModels/Pages/`. For a list page, derive from `EntityBaseVM<TModel>` with ctor `(IFinancistoDatabase, IDialogWrapper)`. For a tag-like page, derive from `TagBasePageVM<TModel>`. Otherwise use `BindableBase, IDataRefresh`. (A page can also come from another project: `ReportsControlVM`/`ReportsControl` live in `Financisto.Reports` and are only registered here, in `GetOrCreatePage` and `ViewLocator.PageViews`.)
2. Add a `case nameof(...)` to `MainWindowVM.GetOrCreatePage(Type)`.
3. Add a `ListItemTemplate` to `ItemsTop` or `ItemsBottom`, with a `LocalizationService.Instance.<key>` label and an `Icon*` resource key. Icons live in `Financisto.Common/Assets/Generic.axaml`.
4. Create `Views/XxxPageView.axaml` with `x:DataType="vm:XxxPageVM"` and register it in `ViewLocator.PageViews`.

### Backup open (`OpenBackup`)

1. `entityReader.ParseBackupFileAsync(path)` runs on a worker thread.
2. The entities are imported into a **new** database (`dbFactory.CreateDatabase()` + `ImportEntitiesAsync`). If that fails, the new database is disposed and the currently loaded one stays intact.
3. `db` is swapped, `_pages.Clear()` is called, and the old database is disposed. Pages are recreated lazily on the next navigation, bound to the new `db`.
4. `_backupVersion` and `_entityColumnsOrder` are stored (saving needs both), and CanExecute is raised on the save and import commands.
5. Keyless entities (`CCardClosingDate`, `CategoryAttribute`, `TransactionAttribute`) are kept in `keyLessEntities`. `ImportEntitiesAsync` inserts only `IIdentity` rows with `Id > 0`, so these rows never reach the DB and are written back as-is on save.
6. `DbManual.ResetAllDatabaseManuals()`, and `DbManual.SetupAsync(db)` (which also loads the import rules) run, the app navigates to the Blotter and shows a toast. If `Settings.ExchangeRates.UpdateOnStart` is set, exchange rates are refreshed.

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
| `DashboardPageVM` | `BindableBase, IDataRefresh` | — | LiveCharts2 `ISeries[]` (structure pie, saldo bars) + accounts-total list via `Services/AccountsTotalService` (port of Android `getAccountsTotal`). The pie and the net worth bars come from one query per date that converts the balances to the home currency with `Common.Utils.ExchangeRateSql` (the same conversion as the Saldo report); an account that can't be converted counts as 0. The pie's slice names go through `ChartText.Label` (LiveCharts can't draw Cyrillic next to an emoji in one text). |
| `AccountsPageVM` | `EntityBaseVM<AccountModel>` | `AccountDialog` / `AccountDialogVM` / `AccountDto`; `AccountInfoDialog` / `AccountInfoDialogVM`; `PurgeAccountDialog` / `PurgeAccountDialogVM` | A new account with an opening amount inserts a transaction (`Id = 0`) and rebuilds the balance. **Delete = Android's `deleteAccount`** (the account and its transactions go, after a "Delete account?" confirmation; see [Account context menu](#account-context-menu)). |
| `BlotterPageVM` | `EntityBaseVM<BlotterModel>` | `TransactionDialog` / `TransactionDialogVM` / `TransactionDto`; `TransferDialog` / `TransferDialogVM` / `TransferDto` | Extra: `AddTransferCommand`, `DuplicateCommand`, `ClearFiltersCommand`, `SelectionChangedCommand` (+`SelectionSummary`). `AddTemplateCommand`/`InfoCommand` are disabled stubs. |
| `CategoriesPageVM` | `EntityBaseVM<CategoryTreeModel>` | `CategoryDialog` / `CategoryDialogVM` / `CategoryDto` | Nested-set tree (`Left`/`Right`). MoveTop/Up/Down/Bottom, SortByTitle rewrite the tree. Restores expand/select state. Delete not implemented. |
| `CurrenciesPageVM` | `EntityBaseVM<CurrencyModel>` | `NewCurrencyDialog` / `NewCurrencyDialogVM` (template picker from `DbManual.AllCurrencies`), then `CurrencyDialog` / `CurrencyDialogVM` / `CurrencyDto` | Delete is blocked while an account or transaction uses the currency. |
| `LocationsPageVM` | `EntityBaseVM<LocationModel>` | `LocationDialog` / `LocationDialogVM : TagDialogVM` / `LocationDto : TagDto` | Own view (address column). Delete not implemented. |
| `PayeesPageVM` | `TagBasePageVM<PayeeModel>` | `TagDialog` / `TagDialogVM` / `TagDto` | `HasAliases = true`. Delete not implemented. |
| `ProjectsPageVM` | `TagBasePageVM<ProjectModel>` | same | Delete not implemented. |
| `TagsPageVM` | `TagBasePageVM<TagModel>` | same | **Bug:** `OnAdd` calls `OpenTagDialogAsync<Project>(0)` (creates a Project). Delete not implemented. |
| `ExchangeRatesPageVM` | `EntityBaseVM<ExchangeRateModel>` | none | Read-only list with From/To currency pickers. `RefreshExchangeRatesCommand` downloads rates via `Services/ExchangeRatesService` (Monobank / OpenExchangeRates / FreeCurrencyRates, chosen in settings). Add/Edit/Delete throw `NotImplementedException`. |
| `SmsTemplatesPageVM` | `EntityBaseVM<SmsTemplateModel>` | `SmsTemplateDialog` / `SmsTemplateDialogVM` / `SmsTemplateDto` (Android-style template), `RuleDialog` / `RuleDialogVM` / `RuleDto` (rule) | All `sms_template` rows: Android templates and desktop rules (`RuleSmsTemplateMapper`). Add template / Add rule / Edit / Delete write to the database right away (reaching the file on Save backup); `RefreshData` re-reads it. Delete has no confirmation. |
| `SettingsPageVM` | `BindableBase, IDataRefresh` | — | Edits a clone of `SettingsService.Current.Settings` (`SettingsDto`: General + ExchangeRates). Save, browse backup dir, check for updates (`UpdateService`, Onova + GitHub releases). Save also pushes `General.IconSet` into `IconSettings.Instance` (see Account icons). The OpenExchangeRates app id is DPAPI-encrypted (`Helpers/SettingsProtection`). |

### BlotterPageVM details

- **Filters** (bound from `BlotterPageView` to Common's filter controls): `PeriodType`, `From`/`To` (`DateTime?`; the `PeriodFilter`'s own `From`/`To` are bound two-way, so a preset such as "Current month" fills them), `Account`, `Category`, `Payee`, `Project`, `Location` (models from `DbManual`; the "all" entry has `Id == null`), and `Tags` (`ObservableCollection<TagModel>`, OR-matched with a substring `Contains`).
- **RefreshData** builds an `Expression<Func<BlotterTransactions,bool>>` with `ExpressionExtensions.And/Or` and passes it to `internal static QueryAsync(db, predicate)`, which queries the `v_blotter` view through `FindManyAndProjectAsync` and projects into `BlotterModel` (currencies and projects resolved from `DbManual.CurrencyIds`/`ProjectIds`); the rows are ordered by date descending. The import also uses `QueryAsync` for the accounts' last transactions.
- **Edit/Duplicate** dispatch on `BlotterModel.Type == "Transfer"`. Duplicate sets `Id = 0` on the parent and every split part.
- **Dialogs and saving live in `Helpers/TransactionEditor`** (internal; shared with the accounts page): `EditTransactionAsync(transaction, subTransactions, currentBalance = null)` / `EditTransferAsync(transfer)` show the dialog, save what it returns and rebuild balances; they return false when cancelled, and the caller refreshes its page. `DeleteTransactionAsync(id)` is the delete below.
- **Save transaction:** `MapperHelper.MapTransaction` → split parts get `Parent`, `FromAccountId` and `ParentAccountId` from the parent. Sub-transfers use `MapperHelper.MapTransfer`; they are **dropped when the parent has a foreign original currency**. Then `InsertOrUpdateAsync(all)`, delete removed parts, `RebuildAccountBalanceAsync` for the from-account and every to-account.
- **Delete** removes the row and its split parts (`Id == id || ParentId == id`), then rebuilds the affected balances.

### Account context menu

Right-clicking a row of the accounts grid opens Android's account quick actions (`AccountListFragment.prepareAccountActionGrid`), as plain text items in Android's order. The `ContextMenu` is on the `DataGrid`; the commands in `AccountsPageVM` all act on `SelectedValue`, and `AccountsPageView.axaml.cs` selects the row under a right-click first (Avalonia doesn't) and cancels the menu (`ContextMenu.Opening`) over the header or empty area.

| Item | Command | What it does (Android behavior) |
|---|---|---|
| Info | `ShowInfoCommand` | `AccountInfoDialog`: type icon, title, type, issuer `#number` (cards), currency, balance, note. A credit card with a limit shows the amount owed and, as the balance, what is left of the limit. **Edit** opens the account dialog. |
| Blotter | `ShowBlotterCommand` | `ShowAccountTransactions(accountId)` (set by `MainWindowVM`): the blotter with every other filter reset and only this account. |
| Edit | `EditCommand` | the account dialog |
| Transaction / Transfer | `AddTransactionCommand` / `AddTransferCommand` | the new transaction / transfer form with this account preselected (`TransactionEditor`) |
| Balance | `UpdateBalanceCommand` | the transaction form in **update balance mode** (below) |
| Delete old transactions | `DeleteOldTransactionsCommand` | `PurgeAccountDialog` (default date: a year and a day ago), a confirmation message, then `db.PurgeAccountAsync`. Android's "database backup first" checkbox is left out: the loaded backup file only changes on Save backup. |
| Close account / Re-open account | `CloseAccountCommand` / `ReopenAccountCommand` | flips `IsActive`; closing asks "Close account?". Only one of the two is visible (`SelectedValue.IsActive`). |
| Delete account | `DeleteCommand` (also the toolbar **Delete** button) | "Delete account?" then `db.DeleteAccountAsync`. This used to be a soft delete (`IsActive = false`), which is now Close account. |
| Transfer current balance | `TransferCurrentBalanceCommand` | the transfer form with the account's whole balance (absolute value) as the amount. Android shows it only when the hidden preference `show_transfer_current_balance` is on (default off); the desktop has no such setting, so it is always there. |

After every change the page calls `ReloadAsync()`: reset and set up `DbManual.Account`, then `RefreshData`. The purge also resets `DbManual.Payee` (the "Previous period" payee may be new).

**Update balance mode** (Android `TransactionActivity.isUpdateBalanceMode`): `TransactionDto.StartBalanceUpdate(balance)` sets `IsUpdateBalance` and `CurrentBalance` and starts the amount at the balance. The amount field is the account's **new balance** (the label reads "Balance (USD)"), `BalanceDifference = RealFromAmount - CurrentBalance` is shown as "Difference", the original-currency row is hidden, and a split's `UnsplitAmount` is measured against the difference. Switching the account in the dialog re-bases `CurrentBalance` on that account (`TransactionDialogVM`, from the balances it was given). Save needs a non-zero difference. On save `TransactionEditor` calls `ApplyBalanceDifference()`, so what is stored is an ordinary transaction whose amount is the difference.

## Dialog system

### IDialogWrapper → DialogWrapper (`Helpers/`)

```csharp
public interface IDialogWrapper
{
    Task<object?> ShowDialogAsync<T>(DialogBaseVM context, double height, double width, string title = null)
        where T : UserControl, new();
    Task<object?> ShowWizardAsync(WizardBaseVM context);                       // WizardWindow; output when finished, else null
    Task<string> OpenFileDialogAsync(string fileExtension);                    // "" when cancelled
    Task<string> SaveFileDialogAsync(string fileExtension, string defaultPath = "");
    Task<string> OpenFolderDialogAsync(string defaultPath = "");
    Task<bool>   ShowMessageBoxAsync(string text, string caption, bool yesNoButtons = false);  // Views/Dialogs/MessageBoxWindow
}
```

`MessageBoxWindow` takes its text and caption from the caller (always pass `LocalizationService` strings) and its **Yes / No / OK** buttons from the `yes` / `no` / `ok` keys (`{loc:Translate}`, so they follow a language change too).

Everything is async and uses Avalonia `StorageProvider` pickers. The owner is `desktop.MainWindow`; without an owner, the calls return `null`, `""` or `true`.

`IToastNotifierWrapper` → `ToastNotifierWrapper` (`ShowMessage`, `ShowWarning`) is built on Message.Avalonia, with a `<msg:MessageHost/>` in `MainWindow.axaml`.

`ReportDialogService : Financisto.Reports.IDialogService` (`Helpers/`) shows a report's message (`ShowMessage(string)`, a `void`) through `IDialogWrapper.ShowMessageBoxAsync`, fire and forget; `MainWindowVM` hands it to `ReportsControlVM`.

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

**Account icons / icon set:** `AccountTypeConverter` (Common) turns account type + card issuer/electronic type into an image. The Settings page's *Icon set* option (`IconSetType`: `Default` = coloured png, `Monocolor` = svg, rasterised to 128px with Svg.Skia) decides which format is tried first; the other format is only the fallback when a file is missing. The current value lives in `IconSettings.Instance` (INotifyPropertyChanged singleton in `Financisto.Common/Utils`), set from the settings in `MainWindow_Loaded` and on Save. Every `AccountTypeConverter` MultiBinding has a third `<Binding Source="{x:Static utils:IconSettings.Instance}" Path="IconSet" />` (passed to the converter as `values[2]`), so changing the setting re-renders all visible icons without reopening pages. New converter usages must include that third binding. Each icon needs both `.png` and `.svg` registered as `AvaloniaResource` in `Financisto.Common.csproj`.

**Account accent color** (Android `AccountActivity` "Accent color code"): a TextBox bound to `Entity.AccentColor` plus an Avalonia `ColorPicker` (package `Avalonia.Controls.ColorPicker`, theme `Themes/Fluent/Fluent.xaml` included in `App.axaml`) bound to `AccountDialogVM.SelectedAccentColor`, opening on the palette tab with Android's 20 colors (`Helpers/AccentColorPalette`, 5×4). The text is the source of truth: it may be a name (`teal`), so a picked color only replaces it when it's a different color, and text→picker updates run under a guard flag so the picker's write-back can't rewrite what the user is typing. Empty/invalid text shows as transparent. `AccountsPageView` draws the color as a gradient behind the icon via `AccentColorBrushConverter`.

**Account icon text** (Android "Text to show as icon"): a TextBox above the accent row bound to `Entity.Icon`. When non-empty, `AccountsPageView` hides the type/issuer image and shows the text instead (FontSize 40, `StringConverters.IsNullOrEmpty`/`IsNotNullOrEmpty`), like Android's `AccountRecyclerAdapter.setAccountIcon`. Android paints it white on its black list; the desktop uses the theme foreground (emoji render in color via font fallback).
| `AccountInfoDialogVM(Account)` | `AccountInfoDialog` | — | read-only; **Edit** (the save command) returns the account id, **Close** cancels |
| `PurgeAccountDialogVM(accountTitle, date)` | `PurgeAccountDialog` | — | returns the chosen `DateTime` |
| `CategoryDialogVM` | `CategoryDialog` | `CategoryDto` | — |
| `SubTransactionDialogVM(TransactionDto)` | `SubTransactionDialog` | `TransactionDto` | `!IsSplitCategory \|\| UnsplitAmount == 0` |
| `TransactionDialogVM(TransactionDto, IDialogWrapper, accountBalances = null) : SubTransactionDialogVM` | `TransactionDialog` | `TransactionDto` | `FromAccount != null && FromAmount != 0 && base` |
| `TransferDialogVM(TransferDto, accountBalances = null)` | `TransferDialog` | `TransferDto` | From and To set && `FromAccountId != ToAccountId` |
| `TagDialogVM(TagDto)` | `TagDialog` | `TagDto` | — (`ShowAliases` = `Entity.SupportsAliases`) |
| `LocationDialogVM : TagDialogVM` | `LocationDialog` | `LocationDto` | — |
| `CurrencyDialogVM` | `CurrencyDialog` | `CurrencyDto` | — |
| `NewCurrencyDialogVM` | `NewCurrencyDialog` | returns `CurrencyTemplateItem` | — |
| `RuleDialogVM(RuleDto)` | `RuleDialog` (420×440) | `RuleDto` | condition valid (description set, or a real MCC title) && at least one action (category/location/project/payee) |

`TransactionDialogVM` opens nested dialogs for split parts: `AddSubTransactionCommand` / `EditSubTransactionCommand` → `SubTransactionDialog`, and `AddSubTransferCommand` → `TransferDialog` with `IsSubTransaction = true`. It edits working copies and copies them back on save. `OpenRecipesDialogCommand` (receipt button in the split section) runs the recipes wizard and appends its `TransactionDto`s as split parts.

**From-account balance:** right before opening `TransactionDialog`/`TransferDialog`, `BlotterPageVM` reads `db.GetLastRunningBalancesAsync()` (account id → last running balance) and passes it to the dialog VM; nothing is cached between dialogs. The VM exposes `FromAccountBalance` (`BlotterUtils.SetAmountText(FromAccountCurrency, balance, false)`, the accounts grid's `AmountTitle` format; `null` for the empty entry or when no balances were passed) and `IsFromAccountBalanceNegative`, re-raised when the DTO's `FromAccount` changes. The views show "Balance: …" under the From account combobox (row height `Auto`, combobox + balance in a StackPanel), DarkGreen / bold DarkRed via the shared `balanceAmount` / `StackPanel.accountBalance` styles in Common `Assets/Styles.axaml` (the accounts grid total uses `balanceAmount` too). Split-part transfers get no balances (small dialog, fixed parent account). When editing, the balance already includes the transaction being edited.

## DTOs

All in `src/Financisto.Desktop/Data/` (namespace `Financisto.Desktop.Data`), Prism `BindableBase`. Each has a constructor from its entity. Writing back is manual: done in the page VM, or in `Helpers/MapperHelper` for transactions and transfers.

- **AccountDto:** Id, Title, Type (string, `AccountType` name), Icon (icon text) and AccentColor (Android color code) — `ApplyDto` trims both and stores `""` for null (NOT NULL columns), CurrencyId, CardIssuer, Issuer, Number, LimitAmount, SortOrder, IsActive, IsIncludeIntoTotals, Note, ClosingDay, PaymentDay, OpeningAmount.
- **BaseTransactionDto** (abstract): `Date` + `Time` → computed `DateTime`, Id, Note, `IsSubTransaction`, virtual `RealFromAmount`, `SubTransactionTitle`, `IsAmountNegative`, `Rate`.
- **TransactionDto : BaseTransactionDto:** FromAccountId/FromAccount (`AccountFilterModel`), CategoryId/Category, PayeeId, ProjectId, LocationId, OriginalCurrencyId/OriginalCurrency, OriginalFromAmount, FromAmount, `SubTransactions` (`ObservableCollection<BaseTransactionDto>`, which holds `TransactionDto` and `TransferDto`), `SelectedTags` (`ObservableCollection<TagModel>`). Computed: `IsSplitCategory` (CategoryId == -1), `SplitAmount`, `UnsplitAmount`, `IsOriginalFromAmountVisible`, `RateString`. Update balance mode: `IsUpdateBalance`, `CurrentBalance`, `BalanceDifference`, `StartBalanceUpdate(balance)`, `ApplyBalanceDifference()`. `TagsDelimiter = "\\n"`.
- **TransferDto : BaseTransactionDto:** FromAccountId/FromAccount, ToAccountId/ToAccount, FromAmount, ToAmount, currencies. Computed: `IsToAmountVisible` (different currencies), `RateString`. `IsAmountNegative` is always `true`, and `RealFromAmount = -|FromAmount|`.
- **CategoryDto:** Id, Title, ParentId, IsIncome, Left, Right.
- **TagDto:** Title, IsActive, and aliases support: `SupportsAliases` (the entity is `IHasAliases`), `Aliases` (one per line), `ApplyAliases(entity)`. **LocationDto : TagDto** adds Address.
- **CurrencyDto:** Id, Title, Name, Symbol, IsDefault, UpdateExchangeRate, Decimals, DecimalSeparator, GroupSeparator, `SymbolFormat` (enum), NumberFormat.
- **SettingsDto** (`SettingsDTO.cs`) holds `SettingsGeneralDto` (CheckForUpdatesOnStart, DefaultBackupDir, Language, CurrentAppTheme, IconSet, ThemeVariant) and `SettingsExchangeRates` (Provider, OpenExchangeRatesProviderAppId, UpdateOnStart).
- **RuleDto:** Condition, Description, MCCCategory, IsActive, Created, and the actions CategoryId/Category, LocationId, PayeeId, ProjectId (`int?`, null = leave unchanged). Built from `RuleModel`; page VMs copy it back into a new `RuleModel`.

`MapperHelper.MapTransaction(dto, tr)` writes FromAmount/OriginalFromAmount with the sign from `IsAmountNegative`, nulls navigation properties, clears `ProjectId` for a split parent, and joins `SelectedTags` with `TagsDelimiter`. `MapperHelper.MapTransfer(dto, tr)` sets `FromAmount = -|x|`, `ToAmount` (own amount when the currencies differ), `OriginalCurrencyId`/`OriginalFromAmount` when the currencies differ, and `CategoryId = 0`.

## Services (`src/Financisto.Desktop/Services/`)

- `SettingsService.Current` is a Cogwheel `SettingsBase` with a source-generated JSON context. It exposes `Settings` (`SettingsDto`), `Load()` and `Save()`.
- `ExchangeRatesService` loads rates from Monobank, OpenExchangeRates (encrypted app id) or FreeCurrencyRates as `CurrencyExchangeRate` entities.
- `UpdateService` wraps Onova `UpdateManager` + `GithubPackageResolver("vov4uk","Financisto.Desktop", "Financisto.Desktop.<rid>.zip")`.
- `AccountsTotalService` + `LatestExchangeRates` compute dashboard totals per currency and in the home currency.

## Bank statement import (`Wizards/`)

Ported from Financier WPF (`Wizards/`, the parsers, `Pages/RulesVM`, `Pages/Dialogs/RuleControl`). The parsers have since become plugins (`src/BankHelpers`, see `bankhelpers_architecture.md`). Financier's tests for this code (parsers against its `Assets` fixtures, wizard flows, rules) live in `src/Tests/Financisto.Desktop.Tests` (`Wizards/`, `Pages/Dialog/RuleDialogVMTest`); the plugin loader's tests are in `Plugins/`.

### Flow (`MainWindowVM.OpenImportWizardAsync(IBankHelper)`)

1. The **Import** menu is built at startup from the plugins folder. `MainWindow.axaml` only has an empty `MenuItem x:Name="ImportMenu"`; `MainWindow.PopulateImportMenu()` fills it from `MainWindowVM.ImportGroups` with one `MenuItem.bankImport` per helper (header bound to `BankImportItem.Title`, `Tag` = the report type shown on the right, icon = `IBankHelper.Icon` decoded to a `Bitmap`) and a `Separator` between report types. Each binds `ImportCommand` with the helper as `CommandParameter`. With no plugins the menu is disabled.
2. `OpenFileDialogAsync(ext)` — the extension is `helper.ReportType.GetFileExtension()` (`csv`/`xlsx`/`pdf`/...).
3. `helper.ParseReport(file)` runs on a worker thread. A parse error is logged and shows the `import_failed` warning toast (no crash).
4. `GetLastTransactionsAsync()`: account id → its latest `v_blotter` row (from `Account.LastTransactionId`; a split part maps to its split parent), for the wizard's hint.
5. `ShowWizardAsync(new MonoWizardVM(bankTitle, rows, lastTransactions, dialogWrapper))` returns `List<Transaction>` (`Id = 0`).
6. `ImportTransactionsAsync` (worker thread): skips rows whose `(FromAccountId, DateTime, FromAmount)` already exists, `db.AddTransactionsAsync`, `RebuildAccountBalanceAsync` for every from/to account, `DbManual` Account cache reset + setup. Then the current page refreshes and the `import_result` / `import_result_with_duplicates` toast shows.

### Parsers (plugins in `src/BankHelpers/Plugins`; the app side is `Helpers/BankHelper/`)

`IBankHelper { BankTitle; ReportType; Icon; ParseReport(path) }` (contract in `Financisto.BankHelpers.Abstractions`). `PluginBankHelperProvider(pluginsDir)` is the `IBankHelperProvider`: it loads every DLL in the folder that references the contract assembly and creates each public `IBankHelper` class with a public parameterless constructor (details: `bankhelpers_architecture.md`). The Desktop csproj builds the plugin projects and copies their DLLs to `plugins/` in the build output and in the publish folder (also inside the macOS bundle's `Contents/MacOS`).

| Plugin project | Helper class (`ReportType`) | Format / library |
|---|---|---|
| `...Monobank` | `MonobankHelper` (Csv) | CSV (CsvHelper) mapped onto `BankTransaction` by `MonobankMap` (a `ClassMap` with English + Ukrainian headers) |
| `...Revolut` | `RevolutHelper` (Csv) | CSV, `RevolutRow` (English + Polish headers) |
| `...Erste` | `ErsteHelper` (Csv) | CSV without a header row (first line is a statement summary: account currency in column 5). Dates only, so `Date` = the second column (transaction date; the first column, booking date, is only the fallback) + minutes by the file's order (rows are newest first; the oldest row of a day is 00:00, hours roll over after 59). The file is sorted by transaction date; card payments book 1-3 days later, so the balance column follows booking order and can differ from the file's order within a day. Card titles are shortened to `<card> 44.37 PLN` + `\r\n` + `<merchant>` (`PŁATNOŚĆ KARTĄ` dropped, other wording like `PRZELEW KARTĄ` kept); a transfer's counterparty is appended unless the title already names it. Foreign-currency card titles (`KARTĄ 25.00 EUR`) fill `OperationAmount/Currency` |
| `...ABank` (two helpers) / `...Privat` | `AbankExcelHelper` (Xlsx) / `PrivatHelper` (Xlsx) | XLSX via MiniExcel → CSV → `AbankRow` / `PrivatRow` → `BankTransaction` (`ABankInfo.ToBankTransaction`, `PrivatHelper.ToBankTransaction`) |
| `...ABank` / `...Pumb` / `...Pireus` | `ABankHelper` / `PumbHelper` / `PireusHelper` (Pdf) : `BankPdfHelperBase` (project `Financisto.BankHelpers.Pdf`) | PDF tables via Tabula (+PdfPig) → CSV → row model |
| `...Pko` | `PkoHelper` (Pdf) | PDF text via Tabula, parsed with regexes (Polish markers such as `Saldo końcowe`) |

`BankTransaction` (`Financisto.BankHelpers`, Monobank's row layout) is the common output. Amounts are `double` in currency units; the wizard converts to minor units. Bank titles come from the plugins (`BankHelperBase.Localized(...)` picks the Ukrainian name for the `uk` UI language); `BankImportItem` re-reads them on a culture change.

### Wizard framework

- `WizardBaseVM` (Prism `BindableBase`): `Pages`, `CurrentPage`, `MoveNextCommand` (CanExecute = `CurrentPage.IsValid()`; on the last page it finishes), `MovePreviousCommand`, `CancelCommand`, `IsOnLastPage`, `Title` (= page title), `RequestClose(output, finished)`. Subclasses implement `CreatePages`, `Before/AfterCurrentPageUpdated` (hand data from one page to the next) and `OnRequestClose` (build the output). The base keeps `WizardPageBaseVM.IsCurrentPage` in sync.
- `WizardWindow` maps page VMs to views in `Window.DataTemplates` (checked before the app-wide `ViewLocator`, which matches every `BindableBase`). **All page views stay alive** in an `ItemsControl` over `Pages`, each with `IsVisible="{Binding IsCurrentPage}"`: a view swapped out of a `ContentControl` has its DataContext cleared, its DataGrid's `ItemsSource` empties first, and the still-attached `SelectedItem` binding writes `null` into the page (the chosen start transaction was lost on Back/Next).

### Import wizard (`MonoWizard/`, used for every bank)

| Page | VM | Content |
|---|---|---|
| 1 | `Page1VM` | Pick the account (`DbManual.Account`); preselects an active account whose title contains the bank name. |
| 2 | `Page2VM` | Statement rows; preselects the row whose balance equals the account balance. Only rows **after** the selected one are imported (none selected: after 2017-11-17). Shows the account balance and last transaction. Delete key removes a row. |
| 3 | `Page3VM` | Editable grid of `FinancistoTransactionDto` rows: from/to account (transfers), category, location, project, payee, note. Rows are pre-filled from the description (location title/address, category title, `*1234` card number → account `Number`, never the imported account itself) and then by the active rules. A note whose first line names the imported account's own card (same last 4 digits as its `Number`) loses that line, e.g. `VISA PLAT 421352******8814 74.00 PLN` before the merchant. Date cell: orange = transfer to an account in another currency (use **Transfer** to enter the other amount), pink = not exactly one of from account / to account / category. **New rule** adds a rule from the selected row and re-applies all rules. |

`MonoWizardVM.OnRequestClose` turns rows into `Transaction`s: to-account set → transfer out of the imported account, from-account set → transfer into it, else an expense/income with the category.

### Recipes wizard (`RecipesWizard/`)

`RecipesVM(totalAmount)`: page 1 takes pasted receipt text (OCR); **Highlight** (`RecipiesHelper.FormatText`) puts each amount (`RecipesFormatter.Pattern`: a number followed by ` A`/`-A`/`Б`/`ГБ` …) at the end of its own line, and the preview (`RecipesFormatter` converter → TextBlock inlines; Avalonia has no RichTextBox) marks amounts yellow and other numbers green. Page 2 edits the parsed rows (category auto-detected from words of the line, amount, note, project). Output: `List<TransactionDto>` split parts. Lines split on any line ending.

### Rules (`DbManual.Rules`, `RuleModel`)

- Stored as `sms_template` rows (`RuleSmsTemplateMapper`): `template` = marker `financisto.desktop.rule` (Android never matches it), condition in `description` as `contains:x` / `matches:x` / `mcc:<Mcc name>`, `0` = not set for the action ids; no `sort_order` (rules apply in id order).
- Condition: `DescriptionContains` / `DescriptionMatches` (case-insensitive, on the row note) or `MCC` (the row's MCC code is in the `Mcc` category's `[MccCodes]`). Every matching active rule is applied in list order, so for each action field the last match wins.
- Loaded once by `DbManual.SetupAsync` on backup open. The SMS templates page saves after each rule add/edit/delete; the import wizard's **New rule** only adds to `DbManual.Rules`, and `OpenImportWizardAsync` saves them once the wizard closes. Nothing is saved on a plain refresh.

## Tests (`src/Tests/`)

Ported from the Financier WPF repo (xunit v3, AutoFixture, Moq). In `Financisto.Desktop.slnx` under `/Tests/`:

- `Financisto.Tests.Common`: shared fixtures (`AutoMoqData`, `PredefinedData`, `JsonDeserializer` for backup-style JSON rows).
- `Financisto.Adapter.Tests`, `Financisto.DataAccess.Tests` (in-memory SQLite through `FinancistoDatabase`), `Financisto.Common.Test` (assembly `Financisto.Converters.Tests`: converter tests; the visibility converters return `bool` for `IsVisible`).
- `Financisto.Desktop.Tests`: VMs (`DashboardPageVMTests` runs the net worth chart on a real in-memory database with only some rates stored), wizards, `Integration/MinBackupIntegrationTests` (imports `Assets/min.backup` into the real in-memory DB and checks that `RebuildAccountBalanceAsync` reproduces the backup's account totals and that open→save writes the same text back, modulo location `0`→`0.0` and exchange-rate row order), bank parsers (`Assets/` statements copied to the output dir; the test project `ProjectReference`s every plugin project), `Plugins/PluginBankHelperProviderTest` (copies single plugin DLLs into a scratch folder and loads them through `PluginBankHelperProvider`, which proves that each DLL carries its own dependencies), `ExchangeRatesService`. It `ProjectReference`s the self-contained win-x64 `Financisto.Desktop` exe, so it has to be `SelfContained` + `win-x64` too (NETSDK1151).
- `Financisto.Reports.Tests`: the report VMs, `ReportsControlVM` and the chart helpers (234 tests, see `reports_architecture.md`, "Tests").

**Running:** `dotnet test` doesn't work here (Microsoft.Testing.Platform reports "Zero tests ran", also in the WPF repo). Build, then run the xunit exe: `src/Tests/<project>/bin/Debug/net10.0[/win-x64]/<AssemblyName>.exe`, optionally `-class Financisto.Desktop.Tests.Pages.BlotterVMIntegrationTests`.

**Test setup in `Financisto.Desktop.Tests/TestEnvironment.cs`** (module initializers): `FINANCISTO_SETTINGS_PATH` points to a scratch dir (`SettingsService.Current` is a static singleton that saves to disk); a background thread owns and pumps `Dispatcher.UIThread`, because `MainWindowVM` navigates through it and would hang otherwise (no Avalonia.Headless needed).

**Porting notes:** WPF's `MainWindowVM.Blotter/Locations/...` are gone; page VMs are built directly (`new BlotterPageVM(db, dialogMock.Object)`), and `MainWindowVM.ImportCommand`/`SaveBackup*` stay disabled until `OpenBackup` ran (`MainWindowVMTest.GetLoadedFinancistoVM`). `IDialogWrapper` is async (`ShowDialogAsync<TDialog>(vm, height, width, title)` with `ReturnsAsync`), `SaveCommand`/`CancelCommand` are toolkit `RelayCommand`s (`CanExecute(null)`), import results are toasts, not message boxes. Running-balance rows store the transaction time, so integration tests zero `Datetime` before comparing (the DTO fixtures carry a `+02:00` offset).

## Leftovers (unused scaffold)

`Models/Categoria.cs`, `Models/Transacao.cs`, `Enum/TipoTransacao.cs`, `Enum/AppTheme.cs` (the settings use Common's `AppThemeType`) and `ViewModels/Charts/*` are not referenced by the running app.
