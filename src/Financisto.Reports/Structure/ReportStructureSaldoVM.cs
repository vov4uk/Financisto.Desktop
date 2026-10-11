using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common.Attribute;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Utils;
using Financisto.Common.Converters;
using Financisto.DataAccess.Abstractions;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace Financisto.Reports
{
    [Header("reports_saldo")]
    public class ReportStructureSaldoVM : BaseReportVM<ReportStructureSaldoModel>
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private ReportStructureSaldoRange _range;
        public ReportStructureSaldoRange Range
        {
            get => _range;
            set
            {
                if (SetProperty(ref _range, value))
                {
                    RaisePropertyChanged(nameof(Range));
                }
            }
        }

        private bool _isUsdCurrencySelected;
        public bool IsUsdCurrencySelected
        {
            get => _isUsdCurrencySelected;
            set
            {
                if (SetProperty(ref _isUsdCurrencySelected, value))
                {
                    RaisePropertyChanged(nameof(IsUsdCurrencySelected));
                }
            }
        }

        private static readonly string BaseSqlText = @" /* ReportStructureSaldoVM */
SELECT account_title,
       account_id,
       account_is_active,
       is_include_into_totals,
       account_type,
       sort_order,
       balance,
       symbol,
       " + ExchangeRateSql.Convert("balance", "currency_id", ExchangeRateSql.HomeCurrencyId, "{0}") + @" AS balance_default_crr,
       " + ExchangeRateSql.Convert("balance", "currency_id", ExchangeRateSql.UsdCurrencyId, "{0}") + @" AS balance_usd,
       default_crr_symbol,
       date
FROM   (SELECT a.title AS account_title,
               a.is_active AS account_is_active,
               a.is_include_into_totals,
               a.sort_order,
               a.type as account_type,
               a._id AS account_id,
               Row_number() OVER ( partition BY a._id
                                   ORDER BY Date(t.datetime / 1000, 'unixepoch') DESC, t.datetime DESC, r.transaction_id DESC
               ) AS RowNum,
               r.balance / 100.0 AS balance,
               c._id AS currency_id,
               c.symbol,
               (SELECT symbol FROM   currency WHERE  is_default = 1) AS default_crr_symbol,
               Date(t.datetime / 1000, 'unixepoch') AS date
        FROM running_balance r
             INNER JOIN account a ON a._id = r.account_id
             INNER JOIN currency c ON a.currency_id = c._id
             INNER JOIN transactions t ON t._id = r.transaction_id
        WHERE t.datetime <= {0}
        ORDER BY a._id, r.datetime DESC ) rep
WHERE RowNum = 1
ORDER BY account_is_active DESC, sort_order ASC

";

        public ReportStructureSaldoVM(IFinancistoDatabase financistoDatabase) : base(financistoDatabase)
        {
            Range = ReportStructureSaldoRange.Last6Months;
            IsUsdCurrencySelected = true;
        }

        protected override async Task RefreshData()
        {
            var currentList = new List<ReportStructureSaldoModel>();
            var unconverted = new SortedSet<string>();
            var availableDates = await GetDatesRange();
            foreach (var date in availableDates)
            {
                var unixDate = new DateTimeOffset(date.ToDateTime(new TimeOnly(00, 00, 00))).ToUnixTimeMilliseconds();
                var filter = $" {unixDate}";
                var sql = string.Format(BaseSqlText, filter);
                var data = await base.db.ExecuteQuery<ReportStructureSaldoRawModel>(sql);

                if (data != null && data.Any())
                {
                    double? assetsDefaultCurrencyBalance = 0;
                    double? liabilitiesDefaultCurrencyBalance = 0;
                    double? assetsUSDBalance = 0;
                    double? liabilitiesUSDBalance = 0;

                    foreach (var item in data.Where(x => x.AccountIsIncludeInTotals))
                    {
                        // an account that can't be converted (no rate found, see ExchangeRateSql) counts as 0 instead of
                        // making the whole total unknown
                        if (item.DefaultCurrencyBalance == null || item.USDBalance == null)
                        {
                            unconverted.Add(item.Title ?? string.Empty);
                        }

                        if (item.AccountType != "LIABILITY")
                        {
                            assetsDefaultCurrencyBalance += item.DefaultCurrencyBalance ?? 0;
                            assetsUSDBalance += item.USDBalance ?? 0;
                        }
                        else
                        {
                            liabilitiesDefaultCurrencyBalance += item.DefaultCurrencyBalance ?? 0;
                            liabilitiesUSDBalance += item.USDBalance ?? 0;
                        }
                    }
                    currentList.Add(new ReportStructureSaldoModel
                    {
                        Date = date,
                        DefaultCurrencySymbol = data.FirstOrDefault()?.DefaultCurrencySymbol,
                        AssetsUSDBalance = Math.Round(assetsUSDBalance ?? 0, 2),
                        AssetsDefaultCurrencyBalance = Math.Round(assetsDefaultCurrencyBalance ?? 0, 2),
                        LiabilitiesUSDBalance = Math.Round(liabilitiesUSDBalance ?? 0, 2),
                        LiabilitiesDefaultCurrencyBalance = Math.Round(liabilitiesDefaultCurrencyBalance ?? 0, 2),
                        NetWorthUSDBalance = Math.Round((assetsUSDBalance ?? 0) + (liabilitiesUSDBalance ?? 0), 2),
                        NetWorthDefaultCurrencyBalance = Math.Round((assetsDefaultCurrencyBalance ?? 0) + (liabilitiesDefaultCurrencyBalance ?? 0), 2),
                    });
                }
            }

            if (unconverted.Count > 0)
            {
                Logger.Warn($"No exchange rate to convert the balance of: {string.Join(", ", unconverted)}");
            }

            Entities = new ObservableCollection<ReportStructureSaldoModel>(currentList);
            Chart = GetChart(Entities.ToList());
        }

        protected override string GetSql()
        {
            if (!DateFilter.HasValue)
            {
                DialogService.ShowMessage(LocalizationService.Instance.please_select_date);
                return string.Empty;
            }

            return string.Format(BaseSqlText, GetStandartTrnFilter());
        }

        private async Task<List<DateOnly>> GetDatesRange()
        {
            var result = new List<DateOnly>();
            var lastDayOfCurrentMonth = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1);
            if (Range == ReportStructureSaldoRange.CurrentYear)
            {
                while (true)
                {
                    result.Add(lastDayOfCurrentMonth);
                    if (lastDayOfCurrentMonth.Month == 1)
                        break;
                    lastDayOfCurrentMonth = lastDayOfCurrentMonth.AddMonths(-1);
                }
            }
            else if (Range == ReportStructureSaldoRange.Last6Months)
            {
                for (var i = 0; i<6; i++)
                {
                    result.Add(lastDayOfCurrentMonth.AddMonths(-i));
                }
            }
            else if (Range == ReportStructureSaldoRange.Last12Months)
            {
                for (var i = 0; i<12; i++)
                {
                    result.Add(lastDayOfCurrentMonth.AddMonths(-i));
                }
            }
            else if (Range == ReportStructureSaldoRange.Last2Years)
            {
                while (true)
                {
                    result.Add(lastDayOfCurrentMonth);
                    if (lastDayOfCurrentMonth.Month == 1 && lastDayOfCurrentMonth.Year == DateTime.Today.Year - 1)
                        break;
                    lastDayOfCurrentMonth = lastDayOfCurrentMonth.AddMonths(-1);
                }
            }
            else if (Range == ReportStructureSaldoRange.Last24Months)
            {
                for (var i = 0; i<24; i++)
                {
                    result.Add(lastDayOfCurrentMonth.AddMonths(-i));
                }
            }
            else if (Range == ReportStructureSaldoRange.AllPeriods)
            {
                var rows = await base.db.ExecuteQuery<FirstTransactionRawModel>("SELECT MIN(datetime) AS first_datetime FROM transactions WHERE is_template = 0");
                var first = rows?.FirstOrDefault()?.FirstDateTime;
                var firstDay = first.HasValue
                    ? DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(first.Value).LocalDateTime)
                    : lastDayOfCurrentMonth;
                var firstMonth = new DateOnly(firstDay.Year, firstDay.Month, 1);

                // one entry per month-end from the current month back to the month of the first transaction
                for (var month = new DateOnly(lastDayOfCurrentMonth.Year, lastDayOfCurrentMonth.Month, 1); month >= firstMonth; month = month.AddMonths(-1))
                {
                    result.Add(month.AddMonths(1).AddDays(-1));
                }
            }

            return result;
        }

        protected override ReportChart GetChart(List<ReportStructureSaldoModel> list)
        {
            string currencySymbol = IsUsdCurrencySelected ? "$" : list.FirstOrDefault()?.DefaultCurrencySymbol ?? string.Empty;

            var source = list.OrderBy(x => x.Date).ToList();

            double Assets(ReportStructureSaldoModel x) => IsUsdCurrencySelected ? x.AssetsUSDBalance ?? 0 : x.AssetsDefaultCurrencyBalance ?? 0;
            double Liabilities(ReportStructureSaldoModel x) => IsUsdCurrencySelected ? x.LiabilitiesUSDBalance ?? 0 : x.LiabilitiesDefaultCurrencyBalance ?? 0;
            double NetWorth(ReportStructureSaldoModel x) => IsUsdCurrencySelected ? x.NetWorthUSDBalance ?? 0 : x.NetWorthDefaultCurrencyBalance ?? 0;

            ISeries[] series =
            [
                new StackedColumnSeries<double>
                {
                    Name = LocalizationService.Instance.assets,
                    Values = source.Select(Assets).ToArray(),
                    Fill = ReportCharts.Fill(ReportCharts.Green),
                },
                new StackedColumnSeries<double>
                {
                    Name = LocalizationService.Instance.liabilities,
                    Values = source.Select(Liabilities).ToArray(),
                    Fill = ReportCharts.Fill(ReportCharts.Amber),
                },
                new LineSeries<double>
                {
                    Name = LocalizationService.Instance.net_worth,
                    Values = source.Select(NetWorth).ToArray(),
                    Fill = null,
                    Stroke = ReportCharts.Stroke(ReportCharts.Gray),
                    GeometryFill = ReportCharts.Fill(SKColors.White),
                    GeometryStroke = ReportCharts.Stroke(ReportCharts.Gray),
                    DataLabelsPaint = ReportCharts.Fill(ReportCharts.Gray),
                    DataLabelsPosition = DataLabelsPosition.Top,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0}{currencySymbol}",
                },
            ];

            Axis[] xAxes = [new Axis { Labels = source.Select(x => x.Date.ToString("yyyy-MM")).ToArray(), MinStep = 1 }];
            Axis[] yAxes = [new Axis { Labeler = value => $"{value:N0}{currencySymbol}" }];

            return new ReportChart(series, xAxes, yAxes);
        }
    }
}
