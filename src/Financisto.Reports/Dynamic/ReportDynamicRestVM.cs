using System;
using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Attribute;
using Financisto.DataAccess.Abstractions;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace Financisto.Reports
{
    [Header("reports_balance_dynamics")]
    public class ReportDynamicRestVM : BaseReportVM<ReportDynamicRestModel>
    {
        private const string BaseSqlText = @" /* ReportDynamicRestVM */
SELECT cr.year  AS year,
       cr.month AS month,
       cr.day   AS day,
       Round(  (
               SELECT Sum(from_amount_default_crr)
               FROM   v_report_transactions trn
               WHERE  Date(trn.datetime / 1000, 'unixepoch') <= cr.date
               AND    to_account_id = 0
               AND    category_id != -1) / 100.00, 2 ) AS total
FROM   (
                       SELECT DISTINCT Date(datetime / 1000, 'unixepoch') AS date,
                                       date_year      AS year,
                                       date_month     AS month,
                                       date_day       AS day
                       FROM            v_report_transactions
                       WHERE           1 = 1 {0}
                                       /* FILTER */
       ) cr
ORDER BY year, month, day";

        public ReportDynamicRestVM(IFinancistoDatabase financistoDatabase) : base(financistoDatabase)
        {
        }

        protected override string GetSql()
        {
            string standartTrnFilter = GetStandartTrnFilter();
            return string.Format(BaseSqlText, !string.IsNullOrEmpty(standartTrnFilter) ? " and " + standartTrnFilter : string.Empty);
        }

        protected override ReportChart GetChart(List<ReportDynamicRestModel> list)
        {
            var green = SKColor.Parse("#4E9A06");

            var points = list
                .OrderBy(x => x.Year).ThenBy(x => x.Month).ThenBy(x => x.Day)
                .Select(x => new DateTimePoint(new DateTime(x.Year, x.Month, x.Day, 0, 0, 0, DateTimeKind.Local), x.Total ?? 0))
                .ToArray();

            ISeries[] series =
            [
                new LineSeries<DateTimePoint>
                {
                    Values = points,
                    Fill = null,
                    Stroke = ReportCharts.Stroke(green, 1),
                    GeometrySize = 4,
                    GeometryFill = ReportCharts.Fill(green),
                    GeometryStroke = ReportCharts.Stroke(green, 1),
                },
            ];

            Axis[] xAxes = [new DateTimeAxis(TimeSpan.FromDays(1), date => date.ToString("yyyy-MM-dd"))];
            Axis[] yAxes = [new Axis { Labeler = value => $"{value:N0}" }];

            return new ReportChart(series, xAxes, yAxes);
        }
    }
}
