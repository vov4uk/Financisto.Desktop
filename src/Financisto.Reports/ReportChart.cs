using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace Financisto.Reports
{
    /// <summary>
    /// The series and axes of one LiveCharts chart. Immutable: a refresh builds a new one and swaps it in whole,
    /// so a chart never renders half-updated series or axes.
    /// </summary>
    public sealed record ReportChart(ISeries[] Series, Axis[] XAxes, Axis[] YAxes)
    {
        /// <summary>Nothing to draw yet (before the first refresh).</summary>
        public static ReportChart Empty { get; } = new([], [new Axis()], [new Axis()]);
    }
}
