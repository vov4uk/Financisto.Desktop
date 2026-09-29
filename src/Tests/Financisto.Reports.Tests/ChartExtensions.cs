namespace Financisto.Reports.Tests
{
    using System.Linq;
    using LiveChartsCore;
    using LiveChartsCore.SkiaSharpView;
    using LiveChartsCore.SkiaSharpView.Painting;
    using SkiaSharp;

    /// <summary>Reads what a report put into its LiveCharts series and axes.</summary>
    internal static class ChartExtensions
    {
        /// <summary>The values of a series, as the <typeparamref name="T"/> it was built with (a double, a nullable double or a DateTimePoint).</summary>
        public static T[] ValuesOf<T>(this ISeries series) =>
            series.Values.Cast<T>().ToArray();

        public static string[] LabelsOf(this Axis axis) => axis.Labels.ToArray();

        /// <summary>The color of a series' <c>Fill</c> or <c>Stroke</c>.</summary>
        public static SKColor ColorOf(object paint) => ((SolidColorPaint)paint).Color;
    }
}
