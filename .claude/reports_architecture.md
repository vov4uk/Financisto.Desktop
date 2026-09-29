# Financisto.Reports — Architecture Reference

## Purpose

An Avalonia class library (`net10.0`) with the **Reports** page: a tree of report kinds on the left and one tab per opened report on the right. Each report runs a SQL query against the in-memory database (`IFinancistoDatabase.ExecuteQuery<T>`) and shows the rows as a **chart** (LiveChartsCore, the same package as the dashboard) or as a **table**.

It is a namespace-renamed port of `Financier.Reports` (WPF + OxyPlot) from the sibling repo `C:\Code\My\Financier.Desktop`. The SQL, the row models, the filters and the VM properties are unchanged; the views are Avalonia and the OxyPlot `PlotModel`s became LiveCharts series (see [Charts](#charts)). References `Financisto.Common` and `Financisto.DataAccess`, plus `LiveChartsCore.SkiaSharpView.Avalonia`. `Financisto.Desktop` references it and hosts `ReportsControl` as a sidebar page.

## Host integration (`Financisto.Desktop`)

- `MainWindowVM.ItemsTop` has a `ListItemTemplate(typeof(ReportsControlVM), …reports, "IconChartBar")`; `GetOrCreatePage` creates `new ReportsControlVM(db, new ReportDialogService(dialogWrapper))`. Like every page it is dropped on backup open (`_pages.Clear()`), so open report tabs are lost when another backup is opened.
- `ViewLocator.PageViews[typeof(ReportsControlVM)] = typeof(ReportsControl)`.
- `Helpers/ReportDialogService` adapts the reports' `IDialogService` to `IDialogWrapper.ShowMessageBoxAsync` (fire and forget: `ShowMessage` is `void`).
- `ReportsControlVM` is not `IDataRefresh`: the page shows no data until a report is opened, and each report refreshes itself.

## ReportsControl / ReportsControlVM

`ReportsControlVM : BindableBase` (Prism, like the other page VMs):

```csharp
ReportsControlVM(IFinancistoDatabase, IDialogService dialogService = null)
List<TreeNode> ReportsInfo                 // 3 groups (Common's TreeNode + [Header] attribute), rebuilt on culture change
ObservableCollection<IReportVM> ReportsVM  // open tabs
IReportVM SelectedReport
ICommand OpenReportCommand(string type)    // TreeNode.Type = the VM's full type name; empty (group node) is ignored
ICommand CloseReportCommand(IReportVM)
```

`OpenReport` selects the tab if that report is already open, else creates the VM with `Activator.CreateInstance(type, db)`, sets `DialogService` and `Header` (the `[Header("key")]` localization key) and selects it. On a culture change the tree is rebuilt and every open tab's `Header` is re-resolved. **Reports don't refresh when opened**: the user presses the refresh button (as in Financier; e.g. the payee/category report needs a filter first).

`SelectedReport` keeps `IReportVM.IsSelected` in sync (true only for the selected report). `CloseReport` selects the report that took the closed tab's place (else the last one, else none): the tab strip alone would jump to the first tab.

`ReportsControl.axaml`: an `Expander` (right) with a `TreeView` of `TreeNode`s (double tap → `OpenReportCommand` through `CommandBehavior.DoubleTappedCommand`), and on the right a horizontally scrollable `TabStrip` over `ReportsVM` (header template: the title and a close button, `Button.closeTab` + `IconXmark`) above an `ItemsControl` over the same collection, whose panel is a plain `Panel`. The VM → view mapping is in `UserControl.DataTemplates` (**checked before the app-wide `ViewLocator`, which matches every `BindableBase`**).

**All opened reports' views stay in the visual tree** and each template sets `IsVisible="{Binding IsSelected}"` on its view (the same keep-alive pattern as the wizard pages). A `TabControl` would only realize the selected tab, so switching tabs would rebuild the views and lose their toolbar state (chart / table switch, open combo boxes, scroll position). State that matters (filters, `Chart`, `Entities`) still lives in the VM.

## Report VMs

`BaseReportVM<T> : BaseViewModel<T>, IReportVM where T : BaseModel, new()` (`T` = the row model):

```csharp
string Header; bool IsSelected; IDialogService DialogService;   // IReportVM (DialogService defaults to NullDialogService: only logs)
ReportChart Chart { get; protected set; }        // main chart, ReportChart.Empty until the first refresh
// filters the shared Common filter controls bind to (index 0 of each DbManual list = "all", Id == null):
Project, Category, TopCategory, Account, Payee, CurentCurrency (sic), StartYearMonths, EndYearMonths,
DateTime? DateFilter, From, To
protected override Task RefreshData();            // GetSql() → db.ExecuteQuery<T> → Entities + Chart = GetChart(rows); skipped when GetSql() is empty
protected abstract string GetSql();               // "" = don't refresh (after telling the user through DialogService)
protected abstract ReportChart GetChart(List<T>);
protected string GetStandartTrnFilter();          // SQL condition from the year/month, date, payee, category (subtree), project, account filters
```

The SQL templates use `string.Format` placeholders and read `v_report_transactions`, `running_balance` and `v_currency_exchange_rate`. **Every `[Column]` on a row model must be in the SELECT** (see Common docs); columns are shown in the table only if the property has `[DisplayName("localization key")]`.

| Report (tab title key) | VM / view | Filters | Row model | Charts |
|---|---|---|---|---|
| Income and expense by month (`reports_by_months`) | `ReportByPeriodMonthCrcVM` / `ReportByPeriodMonthCrc` | currency, account, category, project, payee, from/to month | `ReportByPeriodMonthCrcModel` | clustered columns income + expense, saldo line |
| Assets structure (`reports_assets_structure`) | `ReportStructureActivesVM` / `ReportStructureActives` | date (`DateFilter`, default now) | `ReportStructureActivesModel` | pie of home-currency balances of accounts included in totals; slices < 1% merged into "others". Starts on the grid view |
| Income/expense structure (`reports_income_expense_structure`) | `ReportStructureIncomeExpenseVM` / `ReportStructureIncomeExpense` | expense/income switch (`IsIncome`), from/to month | `ReportStructureIncomeExpenseModel` | `Chart` = row chart of top-level categories (largest on top), `PieChart` |
| By category (`reports_by_category`) | `ByCategoryReportVM` / `ByCategoryReportView` | top category, `PeriodFilter` (`From`/`To`) | `ByCategoryReportModel` (namespace `Financisto.Reports.Structure`) | `Chart` = row chart with income and expense series per category, `PieChart` (net per category) |
| Saldo / net worth (`reports_saldo`) | `ReportStructureSaldoVM` / `ReportStructureSaldo` | currency switch (`IsUsdCurrencySelected`: USD or home), `Range` (`ReportStructureSaldoRange`) | `ReportStructureSaldoModel` (+ `ReportStructureSaldoRawModel` for the query) | clustered assets/liabilities columns + net worth line (only the line has data labels). **Overrides `RefreshData`**: one query per month end (up to 24); `GetSql` is only there for the base class. An account with no exchange rate to USD makes that month's USD totals 0 (a `null` in the sum), as in Financier |
| Dynamics of income/expense (`reports_dynamics_of_expences_incomes`) | `ReportDynamicDebitCretitPayeeVM` / `ReportDynamicDebitCretitPayee` | payee, category, currency, from/to month | `ReportDynamicDebitCretitPayeeModel` | monthly line on a date axis. Needs a payee or a category, else `DialogService.ShowMessage(please_select_categories)` and no refresh |
| Balance dynamics (`reports_balance_dynamics`) | `ReportDynamicRestVM` / `ReportDynamicRest` | from/to month | `ReportDynamicRestModel` | daily line on a date axis |

Quirk kept from Financier: `GetStandartTrnFilter` turns `DateFilter` into a raw unix-ms number (the Actives/Saldo SQL uses it as `{0} BETWEEN …` / `t.datetime <= {0}`), so a date picked in the UI means midnight at its start and that day's own transactions are excluded (the dashboard, which has its own query, includes the whole day).

## Charts

`ReportChart(ISeries[] Series, Axis[] XAxes, Axis[] YAxes)` is an immutable record: a refresh builds a new one and assigns it, so a chart never renders half-updated series (mutating LiveCharts collections in place misrenders, see the dashboard notes). Views bind `lvc:CartesianChart` / `lvc:PieChart` to `Chart.Series`, `Chart.XAxes`, `Chart.YAxes` (a pie has no axes). Reports with two charts expose the second one as `PieChart`; the view shows the chart, the pie or the grid depending on the toolbar's radio buttons (`IsVisible="{Binding #BarBtn.IsChecked}"`).

Report refreshes run from the refresh button, i.e. on the UI thread, so the VMs assign the chart directly. If a report is ever refreshed from a background thread (as the dashboard is by the sidebar navigation), the assignment must go through `Dispatcher.UIThread`.

`ReportCharts` (internal) holds what the reports share: the palette (green `#36B37E` income/assets, amber `#FBBC3D` expense/liabilities, gray line — the dashboard's colors), `Pie(...)` (one `PieSeries<double>` per slice so each has its legend entry "title: 12.34 %"; absolute values, zero slices skipped; percent labels only on slices ≥ 5%) `MonthAxis()` (a `DateTimeAxis` with a 30-day unit and `yyyy-MM` labels) and `BarValueAxis(longestBar)` (a row chart's value axis: starts at 0 and ends 15% past the longest bar, or the data label of that bar is cut off). The month axes of the column charts use `Labels` (`yyyy-MM`) with `MinStep = 1` and no `ForceStepToMin`, so LiveCharts skips labels when there are too many; the category axes of the row charts do use `ForceStepToMin` so every category is labeled. A row chart draws its first item at the bottom, so items are sorted ascending. A category that has no income (or no expense) is a `null` in the `double?` values, which draws no bar.

## Views

Each report view is a `UserControl` with `x:DataType` = its VM (compiled bindings are the project default) and the same shape: a `WrapPanel` toolbar (caption above each control group, since Avalonia has no `GroupBox`) over a `Grid` that stacks the chart(s) and the table. The toolbar uses:

- `ReportStyles.axaml` (`StyleInclude`d by every view): `TextBlock.toolbarCaption`, `RadioButton.iconRadio` (an icon-only radio drawn like a toggle button), `Button.refresh`, `Button.closeTab`.
- Common's filter UserControls (`Filters/*`), which bind loosely to the host DataContext, so the VM property names above matter. Icons come from Common's `Assets/Generic.axaml` (merged into each view, as in the Desktop views).
- `Common/Controls/DataGridAutoHeaders` for the table.

**`PeriodFilter` needs `From`/`To` bound** (`ByCategoryReportView`): the control keeps its own `DateTimeOffset?` `From`/`To`, and choosing a preset ("Current month", …) only sets those, while its date pickers read the host's `From`/`To`. The view binds `From="{Binding From, Mode=TwoWay, Converter=DateTimeToDateTimeOffsetConverter}"` (and `To`) so the presets reach the VM (`BlotterPageView` binds them the same way). `DateTimeToDateTimeOffsetConverter.ConvertBack` returns `null` for a `DateTime?` target when there is no date ("All time"); it used to return `DateTime.MinValue`, which made a "To" of `MinValue` select nothing. `DateFilter` (Assets report) uses the same converter, since the host's `DateFilter` is a `DateTime?` and the `DatePicker` takes a `DateTimeOffset?`.

## Adding a report

1. Row model in the matching folder (`Period/`, `Structure/`, `Dynamic/`): `[Column]` for every SELECT column, `[DisplayName("key")]` for the ones the table shows.
2. VM `[Header("localization key")] XxxVM : BaseReportVM<XxxModel>` with a `(IFinancistoDatabase)` constructor (the host creates it by reflection), `GetSql()` and `GetChart(rows)`.
3. View `Xxx.axaml` (+ code-behind calling `InitializeComponent`) in the shape above, and a `DataTemplate` for the VM in `ReportsControl.axaml`.
4. `new TreeNode(typeof(XxxVM))` in `ReportsControlVM.BuildReportsTree`; the header key (and the tree group's, if new) needs an entry in all three `Resources*.resx`.

## Tests

`src/Tests/Financisto.Reports.Tests` (xunit, copied from Financier) is **not** in `Financisto.Desktop.slnx` and does not compile yet: it asserts on OxyPlot `PlotModel`s / `SafePlotModel`, which no longer exist. It needs porting to `ReportChart` (`Chart.Series`, axes); `IReportVM.DialogService` still works as before. The project has `InternalsVisibleTo` for `Financisto.Reports.Tests` (as does Common, for `DbManual.SetupTests`).
