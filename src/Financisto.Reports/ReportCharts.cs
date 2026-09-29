using System;
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Financisto.Reports
{
    /// <summary>Palette and pieces shared by the report charts. Colors match the dashboard charts.</summary>
    internal static class ReportCharts
    {
        /// <summary>Income / assets.</summary>
        public static readonly SKColor Green = SKColor.Parse("#36B37E");

        /// <summary>Expense / liabilities.</summary>
        public static readonly SKColor Amber = SKColor.Parse("#FBBC3D");

        /// <summary>Saldo / net worth line.</summary>
        public static readonly SKColor Gray = SKColors.Gray;

        /// <summary>Slices smaller than this share of the pie get no percent label inside them.</summary>
        private const double MinLabeledShare = 0.05;

        /// <summary>Extra length of a row chart's value axis past the longest bar, so its data label isn't cut off.</summary>
        private const double LabelRoom = 1.15;

        /// <summary>Value axis of a row chart: starts at 0 and leaves room after the longest bar for its data label.</summary>
        public static Axis BarValueAxis(double longestBar) => new() { MinLimit = 0, MaxLimit = longestBar > 0 ? longestBar * LabelRoom : null };

        /// <summary>X axis for a chart with one point per month.</summary>
        public static Axis MonthAxis() => new DateTimeAxis(TimeSpan.FromDays(30), date => date.ToString("yyyy-MM"));

        public static SolidColorPaint Fill(SKColor color) => new(color);

        public static SolidColorPaint Stroke(SKColor color, float thickness = 2) => new(color, thickness);

        /// <summary>
        /// One pie chart series per slice, so each slice gets its own legend entry ("name: 12.34 %").
        /// Slices are drawn by absolute value (expense totals are negative) and empty ones are skipped.
        /// </summary>
        public static ReportChart Pie(IEnumerable<(string Name, double Value)> slices)
        {
            var items = slices
                .Select(x => (x.Name, Value: Math.Abs(x.Value)))
                .Where(x => x.Value > 0)
                .ToList();
            var total = items.Sum(x => x.Value);

            // the title goes to the legend: outer labels with long titles shrink the pie to nothing
            ISeries[] series = items
                .Select(x => new PieSeries<double>
                {
                    Name = $"{x.Name}: {x.Value / total:P2}",
                    Values = [x.Value],
                    DataLabelsPaint = new SolidColorPaint(SKColors.White),
                    DataLabelsPosition = PolarLabelsPosition.Middle,
                    DataLabelsFormatter = point => point.Coordinate.PrimaryValue / total >= MinLabeledShare
                        ? $"{point.Coordinate.PrimaryValue / total:P0}"
                        : string.Empty,
                })
                .ToArray<ISeries>();

            return new ReportChart(series, [], []);
        }
    }
}
