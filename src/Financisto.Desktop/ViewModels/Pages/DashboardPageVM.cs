using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Financisto.Common;
using Financisto.Common.Localization;
using Financisto.DataAccess.Abstractions;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Models;
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

        // gráficos
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

        //eixos para gráfico de linha
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


        //construtor obrigatório recebendo o serviço
        public DashboardPageVM(IFinancistoDatabase db, IToastNotifierWrapper notifier)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        }

        public IAsyncCommand RefreshDataCommand => _refreshDataCommand ??= new AsyncCommand(RefreshData);

        private async Task RefreshData()
        {
            await StructurePie();
            await SaldoBar();
        }

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

            ISeries[] series =
            [
                new ColumnSeries<double>
                {
                    Name = LocalizationService.Instance.assets,
                    Values = saldo.Select(x => x.AssetsDefaultCurrencyBalance).ToArray(),
                },
                new ColumnSeries<double>
                {
                    Name = LocalizationService.Instance.liabilities,
                    Values = saldo.Select(x => x.LiabilitiesDefaultCurrencyBalance).ToArray(),
                },
                new LineSeries<double>
                {
                    Name = LocalizationService.Instance.net_worth,
                    Values = saldo.Select(x => x.NetWorthDefaultCurrencyBalance).ToArray(),
                    Fill = null,
                    DataLabelsPaint = new SolidColorPaint(SKColors.Gray),
                    DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Top,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0}{symbol}",
                },
            ];
            var xAxis = new Axis { Labels = saldo.Select(x => x.Date.ToString("MMM yyyy", CultureInfo.InvariantCulture)).ToArray() };
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
