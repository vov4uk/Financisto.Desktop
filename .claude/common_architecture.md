# Financisto.Common — Architecture Reference

## Purpose

A shared Avalonia class library (`net10.0`, references `Avalonia`, `Avalonia.Controls.DataGrid`, `Prism.Core` and **Financisto.DataAccess**). It contains base list VMs, async commands, read-only display models, `DbManual` (a static in-memory cache of lookup lists), Avalonia value converters, filter/selector UserControls, attached behaviors, localization, shared styles and icons.

It is a namespace-renamed port of `Financier.Common` (WPF) from the sibling repo `C:\Code\My\Financier.Desktop`. Several WPF idioms were changed during the port; see "Avalonia port rules" below.

## Base VM classes

### BaseViewModel\<T\> (generic list VM base)

```csharp
public abstract class BaseViewModel<T> : BindableBase, IDataRefresh   // Prism BindableBase
    where T : BaseModel, new()
{
    protected readonly IFinancistoDatabase db;
    protected BaseViewModel(IFinancistoDatabase db);
    public ObservableCollection<T> Entities { get; set; }   // lazily created; setter raises PropertyChanged
    public IAsyncCommand RefreshDataCommand { get; }        // cached, wraps RefreshData
    protected abstract Task RefreshData();
}
```

Desktop's `EntityBaseVM<T>` derives from it. Refresh usually **replaces** `Entities` with a new collection.

### IDataRefresh

```csharp
public interface IDataRefresh { IAsyncCommand RefreshDataCommand { get; } }
```

`MainWindowVM` calls it after each navigation.

## Async command pattern

```csharp
public interface IAsyncCommand      { Task ExecuteAsync(); bool CanExecute(); void RaiseCanExecuteChanged(); }
public interface IAsyncCommand<in T>{ Task ExecuteAsync(T p); bool CanExecute(T p); void RaiseCanExecuteChanged(); }

AsyncCommand(Func<Task> action, Func<bool> predicate = null)
AsyncCommand<T>(Func<T, Task> action, Predicate<T> canExecute = null)
```

- Both also implement `System.Windows.Input.ICommand` (a portable BCL type), so Avalonia `Button.Command` binds them directly. `ExecuteAsync` is a no-op when `CanExecute` is false.
- The constructor captures `SynchronizationContext.Current`. `RaiseCanExecuteChanged()` posts to that context, so create commands on the UI thread (they are lazy `??=` properties, normally first touched by a binding).
- Call `RaiseCanExecuteChanged()` yourself; nothing re-queries automatically.

## Display models (`Model/`, namespace `Financisto.Common.Model`)

All derive from `BaseModel` (an empty marker). Two ways they get filled:
- **Raw SQL**: `db.ExecuteQuery<TModel>(sql)` maps columns by `[Column("...")]` (`System.ComponentModel.DataAnnotations.Schema`) attributes. **Every `[Column]` on the model must be in the SELECT**, or `ExecuteQuery` throws.
- **Projection / ctor**: `new AccountModel(Account)`, `new CategoryModel(Category)`, `new CurrencyModel(Currency)`, and `BlotterModel` from a LINQ projection in `BlotterPageVM`.

| Model | Filled from | Notes / computed |
|---|---|---|
| `AccountFilterModel : IActive` | `DbManual` SQL on `account ⨝ currency` | Id, Title, IsActive, SortOrder, Type, CurrencyId, CurrencyName, TotalAmount, LastTransactionId, Number, CardIssuer, Issuer. Used by dropdowns. |
| `AccountModel : AccountFilterModel` | `new AccountModel(Account)` (with `Currency` included) | `Currency` (CurrencyModel), `IsIncludeIntoTotals`, `LastTransactionDate`, `AmountTitle`, `AccountDescription`, `IsTotalAmountNegative` |
| `BlotterModel` | projection of the `v_blotter` view | `Type` ("Transfer" if ToAccountId>0 && CategoryId==0 && FromAccountId>0; "Share" if CategoryId==-1; "Income" if FromAmount>0; else "Expense"), `TransactionTitle`, `AmountTitle`, `BalanceTitle`, `AccountTitle`, `HasNoCategory`, `Tags` (raw), `TagsTitle` (joined " \| ") |
| `CategoryModel` | `DbManual` SQL | Id, Title, Level (computed in SQL), Left, Right, Type |
| `CategoryTreeModel` | built in `CategoriesPageVM` | Id, Left, Right, Title, IsExpanded, IsSelected, `SubCategoties` (sic) |
| `CurrencyModel` | `SELECT * FROM currency` / ctor | all currency columns; `AmountTitle` sample; builds a `NumberFormatInfo` from Java-style `#,##0.00` patterns |
| `TagBaseModel : IActive` | — | Id, Title, IsActive; virtual `AliasesText` (null) |
| `PayeeModel`, `LocationModel : TagBaseModel` | `DbManual` SQL | + `Aliases` (escaped, as in the backup) and `AliasesText` (comma-joined via `BackupText.SplitAliases`). `LocationModel` also has `Address` (`resolved_address`). |
| `ProjectModel`, `TagModel : TagBaseModel` | `DbManual` SQL | `TagModel` + SortOrder |
| `ExchangeRateModel` | `ExchangeRatesPageVM` | FromCurrencyId, ToCurrencyId, Date, `Rate` (double), From/To `CurrencyModel` |
| `YearMonths`, `Years` | `DbManual` SQL over `v_report_transactions` | period pickers |
| `TreeNode` (+ `[Header]`) | — | leftover from Financier's reports tree; currently unused |

`IActive` (`int? Id`, `bool IsActive`, `string Title`) is what the shared `IActive` item template renders (inactive = different brush).

## DbManual — static in-memory lookup cache (`Entities/DbManual.cs`)

```csharp
await DbManual.SetupAsync(db);              // fills every list that is currently null
DbManual.ResetManuals(nameof(DbManual.Payee));   // null one cache (Payee, Location, Project, Tag, Account,
                                                 // Currencies, Category, MCCEnums, MCCTitles)
DbManual.ResetAllDatabaseManuals();         // null every DB-backed cache (on backup open)
```

- **After a write, reset the affected cache and call `SetupAsync` again**, or dropdowns show stale data. Pattern: `ResetManuals(nameof(DbManual.Xxx)); await DbManual.SetupAsync(db); await RefreshData();`
- Lists (all return an empty list when not loaded, never null): `Account`, `Category`, `SubCategory`, `TopCategories`, `Currencies`, `Payee`, `Project`, `Tag`, `Location`, `YearMonths`, `Years`. Lookups: `CurrencyIds`, `ProjectIds` (`Dictionary<int, …>`).
- **Index 0 of every list is an "empty/all" item** with `Id == null` (for Currencies, `Name = all_currencies`). Filters use it as "no filter", and real rows are usually `Where(x => x.Id > 0)`.
- Static data: `MCCEnums`, `MCCTitles`, `MCCCodes` (from the `Mcc` enum attributes) and `AllCurrencies` (embedded `Assets/currencies.csv`, the currency template list).
- XAML binds to the lists directly: `ItemsSource="{x:Static ent:DbManual.Account}"`.
- There are no rules and no `rules.json` in this app (that was Financier).

## Localization (`Localization/`)

```csharp
LocalizationService.Instance["key"]             // indexer: current culture → English → "[key]"
LocalizationService.Instance.delete             // typed property per key (CallerMemberName → indexer)
LocalizationService.Instance.ApplyLanguage(Language.Ukrainian);
```

- Resources: `Resources.resx` (EN, public generated designer) + `.uk.resx` + `.pl.resx`. MCC names are in `ResourcesMcc*.resx`. **A new key needs an entry in all three `.resx` files**, plus a typed property in `LocalizationService` if code uses it.
- `Language` enum: `English`, `Ukrainian`, `Polish`.
- Changing `CurrentCulture` raises `PropertyChanged("CurrentCulture")` **and `PropertyChanged("Item")`**. Avalonia re-reads indexer bindings only for the exact name `"Item"`; WPF's `"Item[]"`, `""` and `null` are ignored. Code that needs to react to a language change listens for one of these names (e.g. `TagBasePageVM` listens for `"Item"`, `ListItemTemplate` for `CurrentCulture`).

XAML:
```xml
xmlns:loc="using:Financisto.Common.Localization"
Text="{loc:Translate Key=save}"                                   <!-- returns Binding("[save]") on the service -->
ItemsSource="{Binding Source={loc:EnumBinding {x:Type ent:AccountType}}}"   <!-- Enum.GetValues -->
```

Enum display text comes from `[LocalizedDescription("key")]` on each value plus `[TypeConverter(typeof(EnumDescriptionTypeConverter))]` on the enum, or from `EnumDescriptionConverter` / `GetEnumDescription()`.

## Converters (`Converters/`)

**Namespace gotcha:** most converters are in namespace `Financisto.Converters` (not `Financisto.Common.Converters`). Only `CategoryTitleConverter`, `LocalizedFormatConverter` and `EnumDescriptionTypeConverter` are in `Financisto.Common.Converters`.

Avalonia specifics: there is no `Visibility` enum, so "…ToVisibility" converters return `bool` for `IsVisible` (the names were kept). `IMultiValueConverter` takes `IList<object>` and is one-way (no ConvertBack).

| Converter | Maps | Notes |
|---|---|---|
| `AmountConverter` | `long` (minor units) ↔ `double` | ÷100, `Math.Abs` unless param `"false"`; ConvertBack ×100 |
| `UnixTimeConverter` | `long` ms ↔ `DateTime` (local) | also **static** `UnixTimeConverter.Convert(long)` / `ConvertBack(DateTime)`, used throughout the code |
| `DateTimeToDateTimeOffsetConverter` | `DateTime` ↔ `DateTimeOffset` | Avalonia `DatePicker.SelectedDate` is `DateTimeOffset?` |
| `DateTimeToTimeSpanConverter` | `DateTime` ↔ `TimeSpan` | `TimePicker.SelectedTime` is `TimeSpan?` |
| `BooleanConverter<T>` | `bool` → T | base for `InverseBooleanConverter`, `InvertedBooleanToVisibilityConverter`, `ActiveStatusBrushConverter`, `ActiveStatusOpacityConverter`, `NoCategoryBackgroundConverter` |
| `NullToBoolConverter` / `NullToVisibilityConverter` | object → `value != null` | MarkupExtensions |
| `StringEmptyToVisibilityConverter` | string → `IsNullOrEmpty` | MarkupExtension |
| `TransactionTypeBrushConverter` / `TransactionTypeIconConverter` | `BlotterModel.Type` → brush / `Icon*` resource | blotter rows |
| `MccConverter` | MCC int → `Mcc` via `DbManual.MCCCodes` | |
| `EnumDescriptionConverter` | Enum → description text | |
| `AccountTypeConverter` (multi) | (type, card_issuer) → `Bitmap` | `avares://Financisto.Common/Assets/AccountType/...png` |
| `CategoryTitleConverter` (multi) | (title, level) → title padded with `-` per level | |
| `LocalizedFormatConverter` (multi) | 2 values → `"Label (value)"`; 3+ → `string.Format` | |
| `DifferentCurrencyConverter`, `OnlyOneSelectedConverter` (multi) | → bool | |

`Assets/Generic.axaml` registers only `categoryTitleConvert`, `localizedFormatConverter` and `activeStatusBrush`, plus the `IActive` DataTemplate, theme brushes and all `Icon*` `DrawingImage`s. Other converters are instantiated locally in each view's resources.

## Controls, filters, behaviors

- `Filters/*` are UserControls with a header `TextBlock` + a picker. Most bind **loosely to the host DataContext** by convention (e.g. `AccountFilter` binds `SelectedItem="{Binding Account}"` with `ItemsSource` = `DbManual.Account`). The csproj sets `AvaloniaUseCompiledBindingsByDefault=false` for this reason. The host VM must expose the matching property names: `Account`, `Category`, `Payee`, `Project`, `Location`, …
- `PeriodFilter` exposes real styled properties: `SelectedPeriodType` (`PeriodType`), `From`/`To` (`DateTimeOffset?`), `Orientation`. Period presets set From/To directly; picker edits merge date and time.
- `TagFilter` / `Controls/TagSelector`: styled property `SelectedTags` (`ObservableCollection<TagModel>`), a flyout of checkboxes over `DbManual.Tag`, header joined with " | ".
- Also: `CategoryFilter`, `TopCategoryFilter`, `CurrencyFilter`, `DateFilter`, `Start/EndYearMonthFilter`, `LocationFilter`, `PayeeFilter`, `ProjectFilter`. The blotter uses Account, Category, Payee, Project, Location, Period and Tag.
- `Behaviors/CommandBehavior.DoubleTappedCommand` (+`…Parameter`) is an attached property that replaces WPF `MouseBinding`.
- `Behaviors/DataGridSelectionBehavior.SelectionChangedCommand` passes `DataGrid.SelectedItems` to the command (used by the blotter selection summary).

## Utility statics (`Utils/`)

- **BlotterUtils:** `TRANSFER_DELIMITER = " » "`, `GetTransferAmountText(fromCur, fromAmt, toCur, toAmt)`, `SetAmountText(currency, amount, addPlus)`, `SetTransferBalanceText(...)`, `GetAccountDescription(issuer, number, type)`.
- **TransactionTitleUtils:** `GenerateTransactionTitle(payee, note, location, categoryId, category, toAccount)` handles split (`-1`), regular and transfer transactions.
- **DoubleUtils:** `GetDouble(text)` (flexible separator), `DoubleEqual`/`DoubleNotEqual`.

## Attributes (`Attribute/`)

- `[LocalizedDescription("key")]` : `DescriptionAttribute`, resolved through `LocalizationService`.
- `[LocalizedMccDescription("key")]` resolves through `ResourcesMcc`. `[MccCodes(params int[])]` maps `Mcc` enum values to numeric MCC codes.
- `[Header("key")]` is only used by the unused `TreeNode`.

## Enums (`Entities/`, namespace `Financisto.Common.Entities`)

`AccountType`, `CardIssuer`, `ElectronicType`, `Mcc`, `SymbolFormat` (RS/R/LS/L; `AppendSymbol` places the symbol around the minus sign), `PeriodType` (AllTime, Today, Yesterday, CurrentWeek, PreviousWeek, PreviousAndCurrentWeek, CurrentMonth, PreviousMonth, PreviousAndCurrentMonth, Custom), `AppThemeType` (System/Light/Dark), `ExchangeRatesProviders` (None, Monobank, OpenExchangeRates, FreeCurrencyRates). `Language` lives in `Localization/`.

## Avalonia port rules (WPF → Avalonia)

- There is no `GroupBox`; use `StackPanel` + header `TextBlock`. There are no `DataTemplate.Triggers`/`DataTrigger`; use a converter binding instead.
- `TextSearch.TextPath="X"` becomes `IsTextSearchEnabled="True"` + `TextSearch.TextBinding="{Binding X}"`. `DisplayMemberPath` becomes `DisplayMemberBinding`.
- Date/time pickers use `DateTimeOffset?` / `TimeSpan?`.
- Resources are loaded with `avares://Financisto.Common/...`. PNG assets must be listed as `AvaloniaResource` in the csproj (they are listed individually).
- `InternalsVisibleTo` is granted to `Financisto.Desktop.Tests` and `Financisto.Reports.Tests`. No such projects exist in the repo; a scratch headless harness can use the `Financisto.Desktop.Tests` assembly name.
