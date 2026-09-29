namespace Financisto.Reports.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.DataAccess.Abstractions;
    using Financisto.Reports.Structure;
    using LiveChartsCore.SkiaSharpView;
    using Moq;
    using Xunit;

    public class ByCategoryReportVMTests
    {
        private readonly Mock<IFinancistoDatabase> dbMock;
        private readonly ByCategoryReportVM vm;

        public ByCategoryReportVMTests()
        {
            this.dbMock = new Mock<IFinancistoDatabase>();
            this.dbMock
                .Setup(x => x.ExecuteQuery<ByCategoryReportModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ByCategoryReportModel>());

            DbManual.SetupTests(new List<CurrencyModel> { new CurrencyModel() });
            DbManual.SetupTests(new List<AccountFilterModel> { new AccountFilterModel() });
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel() });
            DbManual.SetupTests(new List<ProjectModel> { new ProjectModel() });
            DbManual.SetupTests(new List<PayeeModel> { new PayeeModel() });

            this.vm = new ByCategoryReportVM(this.dbMock.Object);
        }

        [Fact]
        public void GetChart_ExpenseItems_AddedToSecondSeries()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Groceries", isExpense: 1, parentId: 1, total: 200.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new double?[] { 200.0 }, chart.Series[1].ValuesOf<double?>());
            Assert.Equal(new double?[] { null }, chart.Series[0].ValuesOf<double?>());
        }

        [Fact]
        public void GetChart_IncomeItems_AddedToFirstSeries()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Salary", isExpense: 0, parentId: 1, total: 500.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new double?[] { 500.0 }, chart.Series[0].ValuesOf<double?>());
            Assert.Equal(new double?[] { null }, chart.Series[1].ValuesOf<double?>());
        }

        [Fact]
        public void GetChart_IncomeAndExpenseOfOneCategory_ShareARow()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Freelance", isExpense: 0, parentId: 1, total: 500.0),
                new TestModel("Freelance", isExpense: 1, parentId: 1, total: -120.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new double?[] { 500.0 }, chart.Series[0].ValuesOf<double?>());
            Assert.Equal(new double?[] { 120.0 }, chart.Series[1].ValuesOf<double?>());
            Assert.Equal(new[] { "Freelance" }, chart.YAxes[0].LabelsOf());
        }

        [Fact]
        public void GetChart_OrdersCategoriesByTheirLargestAbsoluteTotalAscending()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Big", isExpense: 1, parentId: 1, total: -900.0),
                new TestModel("Small", isExpense: 1, parentId: 2, total: -10.0),
                new TestModel("Medium", isExpense: 0, parentId: 3, total: 300.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { "Small", "Medium", "Big" }, chart.YAxes[0].LabelsOf());
            Assert.Equal(new double?[] { null, 300.0, null }, chart.Series[0].ValuesOf<double?>());
            Assert.Equal(new double?[] { 10.0, null, 900.0 }, chart.Series[1].ValuesOf<double?>());
        }

        [Fact]
        public void GetChart_CategoryAxisLabelsAndPieNamesDropEmoji_SoLiveChartsCanDrawTheirCyrillic()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Продукти🥗", isExpense: 1, parentId: 1, total: -100.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { "Продукти" }, chart.YAxes[0].LabelsOf());
            Assert.StartsWith("Продукти: ", testVm.PieChart.Series[0].Name);
        }

        [Fact]
        public void GetChart_PieChart_GroupsByParentId()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Food",     isExpense: 1, parentId: 1, total: 100.0),
                new TestModel("Food",     isExpense: 1, parentId: 1, total: 50.0),
                new TestModel("Transport", isExpense: 1, parentId: 2, total: 80.0),
            };

            testVm.TestGetChart(items);

            Assert.Equal(2, testVm.PieChart.Series.Length);
        }

        [Fact]
        public void GetChart_PieChart_HasOnePieSeriesPerCategoryAndNoAxes()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Food", isExpense: 1, parentId: 1, total: 100.0),
            };

            testVm.TestGetChart(items);

            Assert.Single(testVm.PieChart.Series);
            Assert.IsType<PieSeries<double>>(testVm.PieChart.Series[0]);
            Assert.StartsWith("Food: ", testVm.PieChart.Series[0].Name);
            Assert.Empty(testVm.PieChart.XAxes);
            Assert.Empty(testVm.PieChart.YAxes);
        }

        [Fact]
        public void GetChart_PieChart_SliceValueIsAbsoluteGroupTotal()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Food", isExpense: 1, parentId: 1, total: -100.0),
                new TestModel("Food", isExpense: 1, parentId: 1, total: -50.0),
            };

            testVm.TestGetChart(items);

            Assert.Equal(new[] { 150.0 }, testVm.PieChart.Series[0].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_PieChart_IncomeAndExpenseOfACategoryAreNetted()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Freelance", isExpense: 0, parentId: 1, total: 500.0),
                new TestModel("Freelance", isExpense: 1, parentId: 1, total: -120.0),
            };

            testVm.TestGetChart(items);

            Assert.Equal(new[] { 380.0 }, testVm.PieChart.Series[0].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_ReturnsBarChartWithTwoRowSeries()
        {
            var testVm = new TestableVM(this.dbMock.Object);

            var chart = testVm.TestGetChart(new List<ByCategoryReportModel>());

            Assert.Equal(2, chart.Series.Length);
            Assert.IsType<RowSeries<double?>>(chart.Series[0]);
            Assert.IsType<RowSeries<double?>>(chart.Series[1]);
        }

        [Fact]
        public void GetChart_SeriesAreNamedIncomeAndExpense()
        {
            var testVm = new TestableVM(this.dbMock.Object);

            var chart = testVm.TestGetChart(new List<ByCategoryReportModel>());

            Assert.Equal(LocalizationService.Instance.income, chart.Series[0].Name);
            Assert.Equal(LocalizationService.Instance.expense, chart.Series[1].Name);
        }

        [Fact]
        public void GetChart_ReturnsBarChartWithOneXAxisAndOneYAxis()
        {
            var testVm = new TestableVM(this.dbMock.Object);

            var chart = testVm.TestGetChart(new List<ByCategoryReportModel>());

            Assert.Single(chart.XAxes);
            Assert.Single(chart.YAxes);
        }

        [Fact]
        public void GetChart_ValueAxisStartsAtZeroAndLeavesRoomForTheLongestBarLabel()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Rent", isExpense: 1, parentId: 1, total: -300.0),
                new TestModel("Salary", isExpense: 0, parentId: 2, total: 500.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(0, chart.XAxes[0].MinLimit);
            Assert.True(chart.XAxes[0].MaxLimit > 500.0);
        }

        [Fact]
        public void GetChart_SetsPieChart()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            Assert.Same(ReportChart.Empty, testVm.PieChart);

            testVm.TestGetChart(new List<ByCategoryReportModel>());

            Assert.NotSame(ReportChart.Empty, testVm.PieChart);
        }

        [Fact]
        public void GetChart_UsesAbsoluteValueForBars()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ByCategoryReportModel>
            {
                new TestModel("Rent", isExpense: 1, parentId: 1, total: -300.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new double?[] { 300.0 }, chart.Series[1].ValuesOf<double?>());
        }

        [Fact]
        public void GetSql_ContainsDateBetweenClause()
        {
            var testVm = new TestableVM(this.dbMock.Object);

            var sql = testVm.TestGetSql();

            Assert.Contains("BETWEEN", sql);
        }

        [Fact]
        public void GetSql_NoTopCategory_ContainsTopLevelFilter()
        {
            var testVm = new TestableVM(this.dbMock.Object);

            var sql = testVm.TestGetSql();

            Assert.Contains("parent_level = 0", sql);
        }

        [Fact]
        public void GetSql_WithFromDate_ContainsFromTimestamp()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var fromDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);
            testVm.From = fromDate;

            var sql = testVm.TestGetSql();

            Assert.NotEmpty(sql);
            Assert.Contains("BETWEEN", sql);
            Assert.Contains(new DateTimeOffset(fromDate).ToUnixTimeMilliseconds().ToString(), sql);
        }

        [Fact]
        public void GetSql_WithTopCategory_ContainsLeftRightFilter()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.TopCategory = new CategoryModel { Id = 5, Left = 10, Right = 20 };

            var sql = testVm.TestGetSql();

            Assert.Contains("parent_left > 10", sql);
            Assert.Contains("parent_right < 20", sql);
            Assert.Contains("parent_level = 1", sql);
        }

        [Fact]
        public async Task PieChart_RaisesPropertyChanged_AfterRefresh()
        {
            var raised = false;
            this.vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ByCategoryReportVM.PieChart))
                {
                    raised = true;
                }
            };

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.True(raised);
        }

        [Fact]
        public async Task RefreshDataCommand_EmptyData_EntitiesIsEmpty()
        {
            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Empty(this.vm.Entities);
        }

        [Fact]
        public async Task RefreshDataCommand_ExecutesQueryOnce()
        {
            await this.vm.RefreshDataCommand.ExecuteAsync();

            this.dbMock.Verify(x => x.ExecuteQuery<ByCategoryReportModel>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RefreshDataCommand_SetsPieChart_AfterRefresh()
        {
            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.NotSame(ReportChart.Empty, this.vm.PieChart);
        }

        [Fact]
        public async Task RefreshDataCommand_SetsChart_AfterRefresh()
        {
            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.NotSame(ReportChart.Empty, this.vm.Chart);
        }

        [Fact]
        public async Task RefreshDataCommand_WithData_PopulatesEntities()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ByCategoryReportModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ByCategoryReportModel>
                {
                    new TestModel("Food", isExpense: 1, parentId: 1, total: -100.0),
                    new TestModel("Salary", isExpense: 0, parentId: 2, total: 200.0),
                });

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(2, this.vm.Entities.Count);
        }

        private sealed class TestableVM : ByCategoryReportVM
        {
            public TestableVM(IFinancistoDatabase db)
                : base(db)
            {
            }

            /// <summary>The bar chart; the pie is <see cref="ByCategoryReportVM.PieChart"/>.</summary>
            public ReportChart TestGetChart(List<ByCategoryReportModel> list) =>
                GetChart(list);

            public string TestGetSql() => GetSql();
        }

        private sealed class TestModel : ByCategoryReportModel
        {
            public TestModel(string category, long isExpense, long parentId, double total)
            {
                Category = category;
                IsExpense = isExpense;
                ParentId = parentId;
                Total = total;
            }
        }
    }
}
