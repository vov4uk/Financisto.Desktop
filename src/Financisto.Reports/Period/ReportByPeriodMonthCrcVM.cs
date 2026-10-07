using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Attribute;
using Financisto.Common.Localization;
using Financisto.DataAccess.Abstractions;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace Financisto.Reports
{
    [Header("reports_by_months")]
    public class ReportByPeriodMonthCrcVM : BaseReportVM<ReportByPeriodMonthCrcModel>
    {
        private const string BaseSqlText = @" /* ReportByPeriodMonthCrcVM */
select
    tx.date_year as date_year,
    tx.date_month as date_month,
    round(tx.credit_sum, 2) as credit_sum,
    round(tx.debit_sum, 2) as debit_sum,
    round(tx.credit_sum - tx.debit_sum, 2) as saldo
from (
select
    date_year,
    date_month,
    sum( case when from_amount > 0 then (case when {0} = 1 then from_amount else from_amount_default_crr end ) else 0 end) / 100.00 as credit_sum,
    sum( case when from_amount < 0 then - (case when {0} = 1 then from_amount else from_amount_default_crr end ) else 0 end) / 100.00  as debit_sum
from v_report_transactions
where to_account_id = 0 and (payee_id > 0 or category_id > 0 or project_id > 0)
     /*FILTERS*/
{1}
     /*FILTERS*/
group by
    date_year,
    date_month
order by
    date_year,
    date_month
) tx";

        public ReportByPeriodMonthCrcVM(IFinancistoDatabase financistoDatabase) : base(financistoDatabase)
        {

        }

        protected override string GetSql()
        {
            string str = string.Empty;
            if (CurentCurrency.Id.HasValue)
            {
                str = string.Format("and from_account_crc_id = {0}", CurentCurrency.Id);
            }
            string standartTrnFilter = GetStandartTrnFilter();
            if (standartTrnFilter != string.Empty)
            {
                str = str + " and " + standartTrnFilter;
            }
            return string.Format(BaseSqlText, CurentCurrency.Id.HasValue ? 1 : 0, str);
        }

        protected override ReportChart GetChart(List<ReportByPeriodMonthCrcModel> list)
        {
            ISeries[] series =
            [
                new ColumnSeries<double>
                {
                    Name = LocalizationService.Instance.income,
                    Values = list.Select(x => x.CreditSum ?? 0).ToArray(),
                    Fill = ReportCharts.Fill(ReportCharts.Green),
                },
                new ColumnSeries<double>
                {
                    Name = LocalizationService.Instance.expense,
                    Values = list.Select(x => x.DebitSum ?? 0).ToArray(),
                    Fill = ReportCharts.Fill(ReportCharts.Amber),
                },
                new LineSeries<double>
                {
                    Name = LocalizationService.Instance.saldo,
                    Values = list.Select(x => x.Saldo ?? 0).ToArray(),
                    Fill = null,
                    Stroke = ReportCharts.Stroke(ReportCharts.Gray),
                    GeometryFill = ReportCharts.Fill(SKColors.White),
                    GeometryStroke = ReportCharts.Stroke(ReportCharts.Gray),
                    DataLabelsPaint = ReportCharts.Fill(ReportCharts.Gray),
                    DataLabelsPosition = DataLabelsPosition.Top,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0}",
                },
            ];

            Axis[] xAxes = [new Axis { Labels = list.Select(x => $"{x.Year}-{x.Month:00}").ToArray(), MinStep = 1 }];
            Axis[] yAxes = [new Axis { Labeler = value => $"{value:N0}" }];

            return new ReportChart(series, xAxes, yAxes);
        }
    }
}
