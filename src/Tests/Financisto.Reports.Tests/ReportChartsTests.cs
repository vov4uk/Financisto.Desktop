namespace Financisto.Reports.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Financisto.Common.Localization;
    using LiveChartsCore.SkiaSharpView;
    using SkiaSharp;
    using Xunit;

    public class ReportChartsTests
    {
        [Fact]
        public void Pie_EmptyList_HasNoSeriesAndNoAxes()
        {
            var chart = ReportCharts.Pie(new List<(string Name, double Value)>());

            Assert.Empty(chart.Series);
            Assert.Empty(chart.XAxes);
            Assert.Empty(chart.YAxes);
        }

        [Fact]
        public void Pie_HasOnePieSeriesPerSlice()
        {
            var chart = ReportCharts.Pie(new[] { ("A", 1.0), ("B", 2.0), ("C", 3.0) });

            Assert.Equal(3, chart.Series.Length);
            Assert.All(chart.Series, x => Assert.IsType<PieSeries<double>>(x));
        }

        [Fact]
        public void Pie_SliceIsNamedWithItsTitleAndShare()
        {
            var chart = ReportCharts.Pie(new[] { ("A", 1.0), ("B", 3.0) });

            Assert.Equal($"A: {0.25:P2}", chart.Series[0].Name);
            Assert.Equal($"B: {0.75:P2}", chart.Series[1].Name);
        }

        [Fact]
        public void Pie_KeepsTheOrderOfTheSlices()
        {
            var chart = ReportCharts.Pie(new[] { ("Third", 1.0), ("First", 5.0), ("Second", 3.0) });

            Assert.Equal(new[] { 1.0, 5.0, 3.0 }, chart.Series.Select(x => x.ValuesOf<double>().Single()));
        }

        [Fact]
        public void Pie_DrawsNegativeValuesByTheirAbsoluteValue()
        {
            var chart = ReportCharts.Pie(new[] { ("Food", -100.0), ("Rent", -300.0) });

            Assert.Equal(new[] { 100.0, 300.0 }, chart.Series.Select(x => x.ValuesOf<double>().Single()));
            Assert.Equal($"Food: {0.25:P2}", chart.Series[0].Name);
        }

        [Fact]
        public void Pie_SkipsEmptySlices()
        {
            var chart = ReportCharts.Pie(new[] { ("Empty", 0.0), ("Full", 10.0) });

            var series = Assert.Single(chart.Series);
            Assert.StartsWith("Full: ", series.Name);
        }

        [Fact]
        public void Pie_SlicesOfDifferentSigns_AreAllDrawn()
        {
            var chart = ReportCharts.Pie(new[] { ("Up", 10.0), ("Down", -10.0) });

            Assert.Equal(2, chart.Series.Length);
            Assert.Equal($"Up: {0.5:P2}", chart.Series[0].Name);
        }

        [Fact]
        public void Pie_SliceNameDropsEmoji_SoLiveChartsCanDrawItsCyrillic()
        {
            var chart = ReportCharts.Pie(new[] { ("Продукти🥗", 1.0), ("Транспорт", 1.0) });

            Assert.Equal($"Продукти: {0.5:P2}", chart.Series[0].Name);
            Assert.Equal($"Транспорт: {0.5:P2}", chart.Series[1].Name);
        }

        [Fact]
        public void BarValueAxis_StartsAtZeroAndEndsPastTheLongestBar()
        {
            var axis = ReportCharts.BarValueAxis(200);

            Assert.Equal(0, axis.MinLimit);
            Assert.Equal(230, axis.MaxLimit.Value, 6);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void BarValueAxis_NoPositiveBar_HasNoMaximum(double longestBar)
        {
            var axis = ReportCharts.BarValueAxis(longestBar);

            Assert.Equal(0, axis.MinLimit);
            Assert.Null(axis.MaxLimit);
        }

        [Fact]
        public void MonthAxis_IsADateTimeAxisLabeledWithYearAndMonth()
        {
            var axis = ReportCharts.MonthAxis();

            Assert.IsType<DateTimeAxis>(axis);
            Assert.Equal("2024-03", axis.Labeler(new DateTime(2024, 3, 1).Ticks));
        }

        [Fact]
        public void Fill_IsASolidPaintOfTheColor()
        {
            Assert.Equal(ReportCharts.Green, ChartExtensions.ColorOf(ReportCharts.Fill(ReportCharts.Green)));
        }

        [Fact]
        public void Stroke_IsASolidPaintOfTheColorWithTheThickness()
        {
            var stroke = ReportCharts.Stroke(ReportCharts.Amber, 3);

            Assert.Equal(ReportCharts.Amber, stroke.Color);
            Assert.Equal(3f, stroke.StrokeThickness);
        }

        [Fact]
        public void Palette_IncomeAndExpenseColorsDiffer()
        {
            Assert.NotEqual(ReportCharts.Green, ReportCharts.Amber);
            Assert.NotEqual(SKColors.Transparent, ReportCharts.Gray);
        }

        [Fact]
        public void Empty_HasNoSeriesAndOneDefaultAxisOnEachSide()
        {
            Assert.Empty(ReportChart.Empty.Series);
            Assert.Single(ReportChart.Empty.XAxes);
            Assert.Single(ReportChart.Empty.YAxes);
        }
    }
}
