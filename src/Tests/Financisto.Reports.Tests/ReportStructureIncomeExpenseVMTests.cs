namespace Financisto.Reports.Tests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.DataAccess.Abstractions;
    using LiveChartsCore.SkiaSharpView;
    using Moq;
    using Xunit;

    public class ReportStructureIncomeExpenseVMTests
    {
        private readonly Mock<IFinancistoDatabase> dbMock;
        private readonly ReportStructureIncomeExpenseVM vm;

        public ReportStructureIncomeExpenseVMTests()
        {
            this.dbMock = new Mock<IFinancistoDatabase>();
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureIncomeExpenseModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureIncomeExpenseModel>());

            DbManual.SetupTests(new List<CurrencyModel> { new CurrencyModel() });
            DbManual.SetupTests(new List<AccountFilterModel> { new AccountFilterModel() });
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel() });
            DbManual.SetupTests(new List<ProjectModel> { new ProjectModel() });
            DbManual.SetupTests(new List<PayeeModel> { new PayeeModel() });

            this.vm = new ReportStructureIncomeExpenseVM(this.dbMock.Object);
            this.vm.StartYearMonths = new YearMonths();
            this.vm.EndYearMonths = new YearMonths();
        }

        [Fact]
        public void Constructor_SetsDefaultIsIncome_ToFalse()
        {
            Assert.False(this.vm.IsIncome);
        }

        [Fact]
        public void GetChart_IsIncome_FillColorIsGreen()
        {
            var testVm = CreateTestableVM();
            testVm.IsIncome = true;

            var chart = testVm.TestGetChart(new List<ReportStructureIncomeExpenseModel>());
            var rowSeries = (RowSeries<double>)chart.Series[0];

            Assert.Equal(ReportCharts.Green, ChartExtensions.ColorOf(rowSeries.Fill));
        }

        [Fact]
        public void GetChart_NotIncome_FillColorIsAmber()
        {
            var testVm = CreateTestableVM();
            testVm.IsIncome = false;

            var chart = testVm.TestGetChart(new List<ReportStructureIncomeExpenseModel>());
            var rowSeries = (RowSeries<double>)chart.Series[0];

            Assert.Equal(ReportCharts.Amber, ChartExtensions.ColorOf(rowSeries.Fill));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void GetChart_SeriesIsNamedAfterTheType_SoItsTooltipHasAName(bool isIncome)
        {
            var testVm = CreateTestableVM();
            testVm.IsIncome = isIncome;

            var chart = testVm.TestGetChart(new List<ReportStructureIncomeExpenseModel>());

            Assert.Equal(isIncome ? LocalizationService.Instance.income : LocalizationService.Instance.expense, chart.Series[0].Name);
        }

        [Fact]
        public void GetChart_OrdersItemsByAbsoluteTotalAscending()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureIncomeExpenseModel>
            {
                new TestModel("Big", -300.0),
                new TestModel("Small", -50.0),
                new TestModel("Medium", -150.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 50.0, 150.0, 300.0 }, chart.Series[0].ValuesOf<double>());
            Assert.Equal(new[] { "Small", "Medium", "Big" }, chart.YAxes[0].LabelsOf());
        }

        [Fact]
        public void GetChart_ReturnsOneRowSeries()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportStructureIncomeExpenseModel>());

            Assert.Single(chart.Series);
            Assert.IsType<RowSeries<double>>(chart.Series[0]);
        }

        [Fact]
        public void GetChart_HasOneXAxisAndOneYAxis()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportStructureIncomeExpenseModel>());

            Assert.Single(chart.XAxes);
            Assert.Single(chart.YAxes);
        }

        [Fact]
        public void GetChart_ValueAxisStartsAtZeroAndLeavesRoomForTheLongestBarLabel()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureIncomeExpenseModel>
            {
                new TestModel("Big", -300.0),
                new TestModel("Small", -50.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(0, chart.XAxes[0].MinLimit);
            Assert.True(chart.XAxes[0].MaxLimit > 300.0);
        }

        [Fact]
        public void GetChart_NoItems_ValueAxisHasNoMaximum()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportStructureIncomeExpenseModel>());

            Assert.Null(chart.XAxes[0].MaxLimit);
        }

        [Fact]
        public void GetChart_CategoryAxisIsTitledAndLabelsEveryCategory()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportStructureIncomeExpenseModel>());

            Assert.Equal(LocalizationService.Instance.category, chart.YAxes[0].Name);
            Assert.Equal(1, chart.YAxes[0].MinStep);
            Assert.True(chart.YAxes[0].ForceStepToMin);
        }

        [Fact]
        public void GetChart_UsesAbsoluteValueForBars()
        {
            var testVm = CreateTestableVM();
            testVm.IsIncome = false;
            var items = new List<ReportStructureIncomeExpenseModel>
            {
                new TestModel("Groceries", -200.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 200.0 }, chart.Series[0].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_NullTotal_TreatedAsZero()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureIncomeExpenseModel>
            {
                new TestModel("Unknown", null),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 0.0 }, chart.Series[0].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_WithData_AddsLabelsToAxisAndValuesToSeries()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureIncomeExpenseModel>
            {
                new TestModel("Food", 100.0),
                new TestModel("Transport", 50.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(2, chart.Series[0].ValuesOf<double>().Length);
            Assert.Equal(2, chart.YAxes[0].LabelsOf().Length);
        }

        [Fact]
        public void GetChart_CategoryAxisLabelsDropEmoji_SoLiveChartsCanDrawTheirCyrillic()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureIncomeExpenseModel>
            {
                new TestModel("Продукти🥗", -200.0),
                new TestModel("Транспорт🐌", -100.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { "Транспорт", "Продукти" }, chart.YAxes[0].LabelsOf());
        }

        [Fact]
        public void GetChart_PieChartNamesDropEmoji()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureIncomeExpenseModel> { new TestModel("Продукти🥗", 200.0) };

            testVm.TestGetChart(items);

            Assert.DoesNotContain("🥗", testVm.PieChart.Series[0].Name);
            Assert.StartsWith("Продукти (200", testVm.PieChart.Series[0].Name);
        }

        [Fact]
        public void GetChart_RaisesPieChartPropertyChanged()
        {
            var testVm = CreateTestableVM();
            var raised = false;
            testVm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ReportStructureIncomeExpenseVM.PieChart))
                {
                    raised = true;
                }
            };

            testVm.TestGetChart(new List<ReportStructureIncomeExpenseModel>());

            Assert.True(raised);
        }

        [Fact]
        public void GetChart_SetsPieChart()
        {
            var testVm = CreateTestableVM();
            Assert.Same(ReportChart.Empty, testVm.PieChart);

            testVm.TestGetChart(new List<ReportStructureIncomeExpenseModel>());

            Assert.NotSame(ReportChart.Empty, testVm.PieChart);
        }

        [Fact]
        public void GetChart_PieChartHasOnePieSeriesPerCategory()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureIncomeExpenseModel>
            {
                new TestModel("Food", 300.0),
                new TestModel("Rent", 500.0),
                new TestModel("Utilities", 120.0),
            };

            testVm.TestGetChart(items);

            Assert.Equal(3, testVm.PieChart.Series.Length);
            Assert.All(testVm.PieChart.Series, x => Assert.IsType<PieSeries<double>>(x));
            Assert.StartsWith(items[0].Label + ": ", testVm.PieChart.Series[0].Name);
        }

        [Fact]
        public void GetChart_PieChart_ExpensesAreDrawnByAbsoluteValue()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureIncomeExpenseModel>
            {
                new TestModel("Food", -300.0),
            };

            testVm.TestGetChart(items);

            Assert.Equal(new[] { 300.0 }, testVm.PieChart.Series[0].ValuesOf<double>());
        }

        [Fact]
        public void GetSql_IsIncomeFalse_ContainsLessThanSign()
        {
            var testVm = CreateTestableVM();
            testVm.IsIncome = false;

            var sql = testVm.TestGetSql();

            Assert.Contains("< 0", sql);
        }

        [Fact]
        public void GetSql_IsIncomeTrue_ContainsGreaterThanSign()
        {
            var testVm = CreateTestableVM();
            testVm.IsIncome = true;

            var sql = testVm.TestGetSql();

            Assert.Contains("> 0", sql);
        }

        [Fact]
        public void GetSql_WithNoFilters_DoesNotContainAndClause()
        {
            var testVm = CreateTestableVM();

            var sql = testVm.TestGetSql();

            Assert.DoesNotContain(" and ", sql);
        }

        [Fact]
        public void IsIncome_SetNewValue_RaisesPropertyChanged()
        {
            var raised = false;
            this.vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ReportStructureIncomeExpenseVM.IsIncome))
                {
                    raised = true;
                }
            };

            this.vm.IsIncome = true;

            Assert.True(raised);
        }

        [Fact]
        public void IsIncome_SetSameValue_RaisesPropertyChanged()
        {
            var raised = false;
            this.vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ReportStructureIncomeExpenseVM.IsIncome))
                {
                    raised = true;
                }
            };

            this.vm.IsIncome = false;

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

            this.dbMock.Verify(x => x.ExecuteQuery<ReportStructureIncomeExpenseModel>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RefreshDataCommand_IsExpense_SqlContainsLessThanSign()
        {
            this.vm.IsIncome = false;
            string capturedSql = null;
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureIncomeExpenseModel>(It.IsAny<string>()))
                .Callback<string>(sql => capturedSql = sql)
                .ReturnsAsync(new List<ReportStructureIncomeExpenseModel>());

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Contains("< 0", capturedSql);
        }

        [Fact]
        public async Task RefreshDataCommand_IsIncome_SqlContainsGreaterThanSign()
        {
            this.vm.IsIncome = true;
            string capturedSql = null;
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureIncomeExpenseModel>(It.IsAny<string>()))
                .Callback<string>(sql => capturedSql = sql)
                .ReturnsAsync(new List<ReportStructureIncomeExpenseModel>());

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Contains("> 0", capturedSql);
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
                .Setup(x => x.ExecuteQuery<ReportStructureIncomeExpenseModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureIncomeExpenseModel>
                {
                    new TestModel("Groceries", 100.0),
                    new TestModel("Transport", 50.0),
                });

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(2, this.vm.Entities.Count);
        }

        private TestableVM CreateTestableVM()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.StartYearMonths = new YearMonths();
            testVm.EndYearMonths = new YearMonths();
            return testVm;
        }

        private sealed class TestableVM : ReportStructureIncomeExpenseVM
        {
            public TestableVM(IFinancistoDatabase db)
                : base(db)
            {
            }

            /// <summary>The bar chart; the pie is <see cref="ReportStructureIncomeExpenseVM.PieChart"/>.</summary>
            public ReportChart TestGetChart(List<ReportStructureIncomeExpenseModel> list) =>
                GetChart(list);

            public string TestGetSql() => GetSql();
        }

        private sealed class TestModel : ReportStructureIncomeExpenseModel
        {
            public TestModel(string name, double? total)
            {
                Name = name;
                Total = total;
            }
        }
    }
}
