using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Financisto.Common;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.Common.Utils;
using Financisto.Converters;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Models;
using Financisto.Desktop.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Mvvm;
using SkiaSharp;

namespace Financisto.Desktop.ViewModels.Pages
{
    public class DashboardPageVM : BindableBase, IDataRefresh
    {
        private readonly IFinancistoDatabase db;
        private readonly IToastNotifierWrapper notifier;
        private readonly AccountsTotalService accountsTotalService;

        private IAsyncCommand _refreshDataCommand;

        // balance of every account at the end of the day {0} (unix ms), converted to the home currency
        private const string AccountBalancesSqlText = @" /* DashboardPageVM */
SELECT account_title,
       account_id,
       account_is_active,
       is_include_into_totals,
       account_type,
       sort_order,
       balance_default_crr,
       default_crr_symbol
FROM   (SELECT a.title AS account_title,
               a._id AS account_id,
               a.is_active AS account_is_active,
               a.is_include_into_totals,
               a.type AS account_type,
               a.sort_order,
               Row_number() OVER ( partition BY a._id
                                   ORDER BY Date(t.datetime / 1000, 'unixepoch') DESC, t.datetime DESC
               ) AS RowNum,
               CASE( SELECT _id FROM currency WHERE is_default = 1)
               WHEN c._id THEN r.balance / 100.0
               ELSE Round((r.balance / 100.0 ) * (SELECT rate
                                                  FROM v_currency_exchange_rate
                                                  WHERE to_currency_id = (SELECT _id FROM currency WHERE is_default = 1)
                                                        AND from_currency_id = c._id
                                                        AND(({0} BETWEEN rate_date AND rate_date_end) OR rate_date_end = 253402293599000 )), 0)
               END AS balance_default_crr,
               (SELECT symbol FROM currency WHERE is_default = 1) AS default_crr_symbol
        FROM running_balance r
             INNER JOIN account a ON a._id = r.account_id
             INNER JOIN currency c ON a.currency_id = c._id
             INNER JOIN transactions t ON t._id = r.transaction_id
        WHERE t.datetime <= {0} ) rep
WHERE RowNum = 1
ORDER BY account_is_active DESC, sort_order ASC
";

        // charts
        private ISeries[] _pieSeries = [];
        public ISeries[] PieSeries
        {
            get => _pieSeries;
            private set => SetProperty(ref _pieSeries, value);
        }

        private ISeries[] _lineSeries = [];
        public ISeries[] LineSeries
        {
            get => _lineSeries;
            private set => SetProperty(ref _lineSeries, value);
        }

        // net worth chart axes
        private Axis[] _xAxes = [new Axis()];
        public Axis[] XAxes
        {
            get => _xAxes;
            private set => SetProperty(ref _xAxes, value);
        }

        private Axis[] _yAxes = [new Axis()];
        public Axis[] YAxes
        {
            get => _yAxes;
            private set => SetProperty(ref _yAxes, value);
        }

        // accounts total per currency
        private AccountsTotalItemModel[] _totals = [];
        public AccountsTotalItemModel[] Totals
        {
            get => _totals;
            private set => SetProperty(ref _totals, value);
        }

        private AccountsTotalItemModel _homeCurrencyTotal;
        public AccountsTotalItemModel HomeCurrencyTotal
        {
            get => _homeCurrencyTotal;
            private set => SetProperty(ref _homeCurrencyTotal, value);
        }

        public DashboardPageVM(IFinancistoDatabase db, IToastNotifierWrapper notifier)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            accountsTotalService = new AccountsTotalService(db);
        }

        public IAsyncCommand RefreshDataCommand => _refreshDataCommand ??= new AsyncCommand(RefreshData);

        private async Task RefreshData()
        {
            await StructurePie();
            await AccountsTotal();
            await SaldoBar();
        }

        // same rows as Android AbstractTotalsDetailsActivity: every currency, then the total in the home currency
        private async Task AccountsTotal()
        {
            var totals = await Task.Run(accountsTotalService.GetAccountsTotalsAsync);
            var homeCurrency = totals.HomeCurrency;

            var items = totals.TotalsPerCurrency
                .OrderBy(x => x.Currency.Name, StringComparer.Ordinal)
                .Select(x =>
                {
                    var title = string.Format(LocalizationService.Instance["account_total_in_currency"], x.Currency.Name);
                    return x.Currency.Id == homeCurrency.Id
                        ? CreateAmountItem(x, title)
                        : CreateForeignAmountItem(x, totals.Rates.GetRate(x.Currency.Id ?? 0, homeCurrency.Id ?? 0), homeCurrency, title);
                })
                .ToArray();
            var homeCurrencyTotal = CreateAmountItem(totals.TotalInHomeCurrency, LocalizationService.Instance["home_currency_total"]);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Totals = items;
                HomeCurrencyTotal = homeCurrencyTotal;
            });
        }

        private static AccountsTotalItemModel CreateAmountItem(Total total, string title)
        {
            if (!total.IsError)
            {
                return new AccountsTotalItemModel
                {
                    Title = title,
                    Amount = BlotterUtils.SetAmountText(total.Currency, total.Balance, false),
                    IsNegative = total.Balance < 0,
                };
            }

            return new AccountsTotalItemModel
            {
                Title = title,
                Amount = LocalizationService.Instance["not_available"],
                IsError = true,
                Details = total.Currency.Id == Currency.EMPTY.Id
                    ? LocalizationService.Instance["currency_make_default_warning"]
                    : string.Format(LocalizationService.Instance["rate_not_available_on_date_error"],
                        FormatRateDate(total.Error.DateTime), total.Error.Currency.Name, total.Currency.Name),
            };
        }

        private static AccountsTotalItemModel CreateForeignAmountItem(Total total, ExchangeRate rate, CurrencyModel homeCurrency, string title)
        {
            var item = new AccountsTotalItemModel
            {
                Title = title,
                Amount = BlotterUtils.SetAmountText(total.Currency, total.Balance, false),
                IsNegative = total.Balance < 0,
            };

            if (rate == ExchangeRate.NA)
            {
                return item with
                {
                    IsError = true,
                    Details = string.Format(LocalizationService.Instance["rate_not_available_error"], total.Currency.Name, homeCurrency.Name),
                };
            }

            var rateDate = rate.Date != 0 ? string.Format(LocalizationService.Instance["rate_as_of"], FormatRateDate(rate.Date)) : string.Empty;
            var rateInfo = string.Format(LocalizationService.Instance["rate_info"],
                total.Currency.Name, Math.Abs(rate.Rate).ToString("0.00000", CultureInfo.CurrentCulture), homeCurrency.Name);
            var converted = (long)(decimal)(total.Balance * rate.Rate);

            return item with
            {
                Details = rateDate + rateInfo,
                ConvertedAmount = "≈ " + BlotterUtils.SetAmountText(homeCurrency, converted, false),
            };
        }

        private static string FormatRateDate(long unixTime) => UnixTimeConverter.Convert(unixTime).ToString(UnixTimeConverter.FORMAT_DAY);

        private async Task StructurePie()
        {
            var balances = await Task.Run(() => GetAccountBalancesAsync(DateOnly.FromDateTime(DateTime.Today)));

            var included = balances.Where(x => x.AccountIsIncludeInTotals && x.DefaultCurrencyBalance > 0.0).ToList();
            var total = included.Sum(x => x.DefaultCurrencyBalance ?? 0.0);

            // accounts below 1% of the total are merged into a single "others" slice
            var others = included.Where(x => x.DefaultCurrencyBalance / total < 0.01).ToList();
            var slices = included.Except(others)
                .Select(x => (Title: x.Title, Balance: x.DefaultCurrencyBalance ?? 0.0))
                .ToList();
            if (others.Count > 0)
            {
                slices.Add((LocalizationService.Instance.others, others.Sum(x => x.DefaultCurrencyBalance ?? 0.0)));
            }

            ISeries[] series = slices
                .Select(x => new PieSeries<double>
                {
                    // outer labels with long account titles shrink the pie to nothing, so the title goes to the legend
                    Name = $"{x.Title}: {x.Balance / total:P2}",
                    Values = [x.Balance],
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => point.Coordinate.PrimaryValue / total >= 0.05 ? $"{point.Coordinate.PrimaryValue / total:P0}" : string.Empty,
                })
                .ToArray<ISeries>();

            await Dispatcher.UIThread.InvokeAsync(() => PieSeries = series);
        }

        private async Task SaldoBar()
        {
            var saldo = await Task.Run(() => GetSaldoAsync(GetLastMonthsEndDates(12)));
            var symbol = saldo.FirstOrDefault()?.DefaultCurrencySymbol ?? string.Empty;

            // assets and liabilities share one column: positive values stack above the axis, negative below it
            ISeries[] series =
            [
                new StackedColumnSeries<double>
                {
                    Name = LocalizationService.Instance.assets,
                    Values = saldo.Select(x => x.AssetsDefaultCurrencyBalance).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#36B37E")),
                },
                new StackedColumnSeries<double>
                {
                    Name = LocalizationService.Instance.liabilities,
                    Values = saldo.Select(x => x.LiabilitiesDefaultCurrencyBalance).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse("#FBBC3D")),
                },
                new LineSeries<double>
                {
                    Name = LocalizationService.Instance.net_worth,
                    Values = saldo.Select(x => x.NetWorthDefaultCurrencyBalance).ToArray(),
                    Fill = null,
                    Stroke = new SolidColorPaint(SKColors.Gray, 2),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColors.Gray, 2),
                    DataLabelsPaint = new SolidColorPaint(SKColors.Gray),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0}{symbol}",
                },
            ];
            var xAxis = new Axis { Labels = saldo.Select(x => x.Date.ToString("MMM yyyy", CultureInfo.CurrentUICulture)).ToArray() };
            var yAxis = new Axis { Labeler = value => $"{value:N0}{symbol}" };

            // navigation refreshes pages from a thread-pool thread; the chart must be updated on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                XAxes = [xAxis];
                YAxes = [yAxis];
                LineSeries = series;
            });
        }

        private async Task<List<ReportStructureSaldoModel>> GetSaldoAsync(IEnumerable<DateOnly> dates)
        {
            var result = new List<ReportStructureSaldoModel>();
            foreach (var date in dates.OrderBy(x => x))
            {
                var data = await GetAccountBalancesAsync(date);
                if (data.Count == 0)
                {
                    continue;
                }

                double assets = 0;
                double liabilities = 0;
                foreach (var item in data.Where(x => x.AccountIsIncludeInTotals))
                {
                    if (item.AccountType != "LIABILITY")
                    {
                        assets += item.DefaultCurrencyBalance ?? 0;
                    }
                    else
                    {
                        liabilities += item.DefaultCurrencyBalance ?? 0;
                    }
                }

                result.Add(new ReportStructureSaldoModel
                {
                    Date = date,
                    DefaultCurrencySymbol = data[0].DefaultCurrencySymbol,
                    AssetsDefaultCurrencyBalance = Math.Round(assets, 2),
                    LiabilitiesDefaultCurrencyBalance = Math.Round(liabilities, 2),
                    NetWorthDefaultCurrencyBalance = Math.Round(assets + liabilities, 2),
                });
            }

            return result;
        }

        private Task<List<AccountBalanceRawModel>> GetAccountBalancesAsync(DateOnly date)
        {
            // include the whole day
            var unixDate = new DateTimeOffset(date.AddDays(1).ToDateTime(TimeOnly.MinValue)).ToUnixTimeMilliseconds() - 1;
            return db.ExecuteQuery<AccountBalanceRawModel>(string.Format(AccountBalancesSqlText, unixDate));
        }

        private static IEnumerable<DateOnly> GetLastMonthsEndDates(int count)
        {
            var firstDayOfNextMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1);
            return Enumerable.Range(0, count).Select(i => firstDayOfNextMonth.AddMonths(-i).AddDays(-1));
        }
    }
}
