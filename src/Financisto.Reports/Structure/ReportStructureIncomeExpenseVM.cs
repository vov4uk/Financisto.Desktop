using System;
using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Attribute;
using Financisto.Common.Localization;
using Financisto.Common.Utils;
using Financisto.DataAccess.Abstractions;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;

namespace Financisto.Reports
{
    [Header("reports_income_expense_structure")]
    public class ReportStructureIncomeExpenseVM : BaseReportVM<ReportStructureIncomeExpenseModel>
    {
        private bool isIncome;

        public bool IsIncome
        {
            get => isIncome;
            set
            {
                isIncome = value;
                RaisePropertyChanged(nameof(IsIncome));
            }
        }

        private ReportChart pieChart = ReportChart.Empty;

        /// <summary>The same totals as a pie; <see cref="BaseReportVM{T}.Chart"/> is the bar chart.</summary>
        public ReportChart PieChart
        {
            get => pieChart;
            private set
            {
                pieChart = value;
                RaisePropertyChanged(nameof(PieChart));
            }
        }

        private const string BaseSqlText = @" /* ReportStructureDebitVM */
SELECT p.title                                            AS title,
       Round(Sum(tx.from_amount_default_crr) / 100.00, 2) AS total
FROM   (SELECT (SELECT parent._id AS _id
                FROM   category AS node,
                       category AS parent
                WHERE  node.LEFT BETWEEN parent.LEFT AND parent.right
                       AND node._id = t.category_id
                ORDER  BY parent.LEFT ASC
                LIMIT  1) top_parent,
               t.from_amount_default_crr
        FROM   v_report_transactions t
        WHERE  category_id > 0 AND from_account_is_include_into_totals = 1
        AND t.from_amount {0} 0
/*FILTERS*/
               {1}
/*FILTERS*/
       ) tx
       INNER JOIN category p
               ON p._id = tx.top_parent
GROUP  BY p._id
ORDER  BY total ASC ";

        public ReportStructureIncomeExpenseVM(IFinancistoDatabase financistoDatabase) : base(financistoDatabase)
        {

        }

        protected override string GetSql()
        {
            string str = string.Empty;
            string standartTrnFilter = GetStandartTrnFilter();
            if (standartTrnFilter != string.Empty)
            {
                str = " and " + standartTrnFilter;
            }

            string sign = IsIncome ? ">" : "<";

            return string.Format(BaseSqlText, sign, str);
        }

        protected override ReportChart GetChart(List<ReportStructureIncomeExpenseModel> list)
        {
            PieChart = ReportCharts.Pie(list.Select(x => (x.Label, x.Total ?? 0)));
            return GetBarChart(list);
        }

        private ReportChart GetBarChart(List<ReportStructureIncomeExpenseModel> list)
        {
            // largest category on top: a row chart draws its first item at the bottom
            var items = list.OrderBy(x => Math.Abs(x.Total ?? 0)).ToList();
            var sign = IsIncome ? string.Empty : "-";
            var labels = items.Select(x => ChartText.Label(x.Name)).ToArray();

            // a row chart's tooltip needs its texts spelled out: by default it shows just the series name.
            // The "X" text is the tooltip's header (here the category), the "Y" one the series line's value.

            ISeries[] series =
            [
                new RowSeries<double>
                {
                    Name = IsIncome ? LocalizationService.Instance.income : LocalizationService.Instance.expense,
                    Values = items.Select(x => Math.Abs(x.Total ?? 0)).ToArray(),
                    Fill = ReportCharts.Fill(IsIncome ? ReportCharts.Green : ReportCharts.Amber),
                    DataLabelsPaint = ReportCharts.Fill(ReportCharts.Gray),
                    DataLabelsPosition = DataLabelsPosition.End,
                    DataLabelsFormatter = point => $"{sign}{point.Coordinate.PrimaryValue}",
                    XToolTipLabelFormatter = point => labels[point.Index],
                    YToolTipLabelFormatter = point => $"{sign}{point.Coordinate.PrimaryValue:N2}",
                },
            ];

            Axis[] xAxes = [ReportCharts.BarValueAxis(items.Select(x => Math.Abs(x.Total ?? 0)).DefaultIfEmpty().Max())];
            Axis[] yAxes =
            [
                new Axis
                {
                    Name = LocalizationService.Instance.category,
                    Labels = labels,
                    MinStep = 1,
                    ForceStepToMin = true,
                },
            ];

            return new ReportChart(series, xAxes, yAxes);
        }
    }
}
