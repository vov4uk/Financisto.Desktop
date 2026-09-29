using System;
using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Attribute;
using Financisto.Common.Localization;
using Financisto.DataAccess.Abstractions;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace Financisto.Reports
{
    [Header("reports_dynamics_of_expences_incomes")]
    public class ReportDynamicDebitCretitPayeeVM : BaseReportVM<ReportDynamicDebitCretitPayeeModel>
    {
        private const string BaseSqlText = @" /* ReportDynamicDebitCretitPayeeVM */
 select
    tx.date_year as date_year,
    tx.date_month as date_month,
    round(tx.total / 100.00, 2) as total
from
    (select
        trn.date_year,
        trn.date_month,
        trn.category_id,
        sum(case when {0} = 1 then from_amount else from_amount_default_crr end) as total
    from v_report_transactions trn
    where (payee_id > 0 or category_id > 0 or project_id > 0)
    /*FILTERS*/
        {1}
    /*FILTERS*/
    group by
        trn.date_year,
        trn.date_month ) tx
order by
    tx.date_year,
    tx.date_month";

        public ReportDynamicDebitCretitPayeeVM(IFinancistoDatabase financistoDatabase) : base(financistoDatabase)
        {
        }

        protected override string GetSql()
        {
            long? categoryId;
            int hasCategory;
            if (!Payee.Id.HasValue)
            {
                categoryId = Category.Id;
                hasCategory = categoryId.HasValue ? 1 : 0;
            }
            else
            {
                hasCategory = 1;
            }

            if (hasCategory == 0)
            {
                DialogService.ShowMessage(LocalizationService.Instance.please_select_categories);
                return string.Empty;
            }
            string str = string.Empty;
            categoryId = CurentCurrency.Id;
            if (categoryId.HasValue)
            {
                str = string.Format(" and from_account_crc_id = {0}", CurentCurrency.Id);
            }
            string standartTrnFilter = GetStandartTrnFilter();
            if (standartTrnFilter != string.Empty)
            {
                str = str + " and " + standartTrnFilter;
            }
            categoryId = CurentCurrency.Id;
            return string.Format(BaseSqlText, categoryId.HasValue ? 1 : 0, str);
        }

        protected override ReportChart GetChart(List<ReportDynamicDebitCretitPayeeModel> list)
        {
            // TODO - add title (selected payee + category)
            var points = list
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .Select(x => new DateTimePoint(new DateTime(x.Year, x.Month, 1, 0, 0, 0, DateTimeKind.Local), x.Total ?? 0))
                .ToArray();

            ISeries[] series =
            [
                new LineSeries<DateTimePoint>
                {
                    Values = points,
                    Fill = null,
                    Stroke = ReportCharts.Stroke(ReportCharts.Green),
                    GeometryFill = ReportCharts.Fill(SKColors.White),
                    GeometryStroke = ReportCharts.Stroke(ReportCharts.Green),
                    DataLabelsPaint = ReportCharts.Fill(ReportCharts.Gray),
                    DataLabelsPosition = DataLabelsPosition.Top,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue:N0}",
                },
            ];

            Axis[] xAxes = [ReportCharts.MonthAxis()];
            Axis[] yAxes = [new Axis { Labeler = value => $"{value:N0}" }];

            return new ReportChart(series, xAxes, yAxes);
        }
    }
}
