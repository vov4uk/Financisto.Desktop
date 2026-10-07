using System;
using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Attribute;
using Financisto.Common.Localization;
using Financisto.Common.Utils;
using Financisto.Converters;
using Financisto.DataAccess.Abstractions;
using Financisto.Reports.Structure;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;

namespace Financisto.Reports
{
    [Header("reports_by_category")]
    public class ByCategoryReportVM : BaseReportVM<ByCategoryReportModel>
    {
        private const string BaseSqlText = @" /* ByCategoryReportModel */
SELECT parent_id,
       parent_title,
       is_expense,
       Round(Sum(tx.from_amount_default_crr) / 100.00, 2) AS total
FROM   (
        SELECT ctgr.parent_id,
               parent_level,
               parent_title,
               t.category_id,
               parent_left,
               parent_right,
               t.from_amount_default_crr,
               cast(t.from_amount < 0 as boolean) as is_expense
        FROM   v_report_transactions t
        INNER JOIN (SELECT parent._id AS parent_id,
                           parent.title as parent_title,
                           parent.left as parent_left,
                           parent.right as parent_right,
                           node._id as node_id,
                           node.title as node_title,
                           (select count(*) from category x where x.left < node.left and x.[right] > node.[right] ) as node_level,
                           (select count(*) from category x where x.left < parent.left and x.[right] > parent.[right] ) as parent_level
                FROM   category AS node,
                       category AS parent
                WHERE  node.LEFT BETWEEN parent.LEFT AND parent.right AND parent.right != node._id
                ORDER  BY parent.LEFT ASC ) ctgr
                on ctgr.node_id = t.category_id
        WHERE  t.category_id > 0 AND from_account_is_include_into_totals = 1
/*FILTERS*/
               {0} ) tx
WHERE  {1}
/*FILTERS*/
GROUP  BY parent_id,  parent_title , is_expense
ORDER  BY total ASC ";

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

        public ByCategoryReportVM(IFinancistoDatabase financistoDatabase)
            : base(financistoDatabase)
        {
        }

        protected override ReportChart GetChart(List<ByCategoryReportModel> list)
        {
            PieChart = GetPieChart(list);
            return GetBarChart(list);
        }

        /// <summary>Rows grouped per category (income and expense of one category together), smallest category first.</summary>
        private static List<IGrouping<long, ByCategoryReportModel>> GroupByCategory(List<ByCategoryReportModel> list) =>
            list.GroupBy(x => x.ParentId)
                .OrderBy(x => x.Max(y => Math.Abs(y.Total)))
                .ToList();

        private static ReportChart GetPieChart(List<ByCategoryReportModel> list) =>
            ReportCharts.Pie(GroupByCategory(list).Select(x => (x.First().Category, x.Sum(y => y.Total))));

        private static ReportChart GetBarChart(List<ByCategoryReportModel> list)
        {
            var groups = GroupByCategory(list);
            var labels = groups.Select(x => ChartText.Label(x.First().Category)).ToArray();

            // one value per category and null where the category has no income (or expense), so no empty bar is drawn
            double?[] Totals(bool isExpense) => groups
                .Select(x =>
                {
                    var rows = x.Where(y => (y.IsExpense != 0) == isExpense).ToList();
                    return rows.Count == 0 ? (double?)null : Math.Abs(rows.Sum(y => y.Total));
                })
                .ToArray();

            ISeries[] series =
            [
                new RowSeries<double?>
                {
                    Name = LocalizationService.Instance.income,
                    Values = Totals(isExpense: false),
                    Fill = ReportCharts.Fill(ReportCharts.Green),
                    DataLabelsPaint = ReportCharts.Fill(ReportCharts.Gray),
                    DataLabelsPosition = DataLabelsPosition.End,
                    DataLabelsFormatter = point => $"{point.Coordinate.PrimaryValue}",
                    XToolTipLabelFormatter = point => labels[point.Index],
                    YToolTipLabelFormatter = point => $"{point.Coordinate.PrimaryValue:N2}",
                },
                new RowSeries<double?>
                {
                    Name = LocalizationService.Instance.expense,
                    Values = Totals(isExpense: true),
                    Fill = ReportCharts.Fill(ReportCharts.Amber),
                    DataLabelsPaint = ReportCharts.Fill(ReportCharts.Gray),
                    DataLabelsPosition = DataLabelsPosition.End,
                    DataLabelsFormatter = point => $"-{point.Coordinate.PrimaryValue}",
                    XToolTipLabelFormatter = point => labels[point.Index],
                    YToolTipLabelFormatter = point => $"-{point.Coordinate.PrimaryValue:N2}",
                },
            ];

            Axis[] xAxes = [ReportCharts.BarValueAxis(list.Select(x => Math.Abs(x.Total)).DefaultIfEmpty().Max())];
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

        protected override string GetSql()
        {
            var fromUnix = UnixTimeConverter.ConvertBack(From ?? DateTime.MinValue.ToLocalTime());
            var toUnix = UnixTimeConverter.ConvertBack(To ?? DateTime.MaxValue.ToLocalTime());

            var dateFilter = $"AND t.datetime BETWEEN {fromUnix} AND {toUnix}";
            string str = this.TopCategory?.Id == null
                ? "parent_level = 0"
                : $"parent_left > {TopCategory.Left} AND parent_right < {TopCategory.Right} AND parent_level = 1";

            return string.Format(BaseSqlText, dateFilter, str);
        }
    }
}
