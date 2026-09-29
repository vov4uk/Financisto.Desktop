namespace Financisto.Reports.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.DataAccess.Abstractions;
    using LiveChartsCore.Defaults;
    using LiveChartsCore.SkiaSharpView;
    using Moq;
    using Xunit;

    public class ReportDynamicRestVMTests
    {
        private readonly Mock<IFinancistoDatabase> dbMock;
        private readonly ReportDynamicRestVM vm;

        public ReportDynamicRestVMTests()
        {
            this.dbMock = new Mock<IFinancistoDatabase>();
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportDynamicRestModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportDynamicRestModel>());

            DbManual.SetupTests(new List<CurrencyModel> { new CurrencyModel() });
            DbManual.SetupTests(new List<AccountFilterModel> { new AccountFilterModel() });
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel() });
            DbManual.SetupTests(new List<ProjectModel> { new ProjectModel() });
            DbManual.SetupTests(new List<PayeeModel> { new PayeeModel() });

            this.vm = new ReportDynamicRestVM(this.dbMock.Object);
            this.vm.StartYearMonths = new YearMonths();
            this.vm.EndYearMonths = new YearMonths();
        }

        [Fact]
        public void GetChart_XAxisIsDateTimeAxis()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportDynamicRestModel>());

            Assert.IsType<DateTimeAxis>(chart.XAxes[0]);
        }

        [Fact]
        public void GetChart_HasOneLineSeries()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportDynamicRestModel>());

            Assert.Single(chart.Series);
            Assert.IsType<LineSeries<DateTimePoint>>(chart.Series[0]);
        }

        [Fact]
        public void GetChart_HasOneXAxisAndOneYAxis()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportDynamicRestModel>());

            Assert.Single(chart.XAxes);
            Assert.Single(chart.YAxes);
        }

        [Fact]
        public void GetChart_NullTotal_TreatedAsZero()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportDynamicRestModel>
            {
                new TestModel(year: 2024, month: 1, day: 1, total: null),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(0.0, chart.Series[0].ValuesOf<DateTimePoint>()[0].Value);
        }

        [Fact]
        public void GetChart_OrdersPointsByDay_WhenSameYearAndMonth()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportDynamicRestModel>
            {
                new TestModel(year: 2024, month: 1, day: 20, total: 200.0),
                new TestModel(year: 2024, month: 1, day: 5,  total: 500.0),
                new TestModel(year: 2024, month: 1, day: 15, total: 150.0),
            };

            var chart = testVm.TestGetChart(items);
            var points = chart.Series[0].ValuesOf<DateTimePoint>();

            Assert.Equal(500.0, points[0].Value);
            Assert.Equal(150.0, points[1].Value);
            Assert.Equal(200.0, points[2].Value);
        }

        [Fact]
        public void GetChart_OrdersPointsByYearThenMonthThenDay()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportDynamicRestModel>
            {
                new TestModel(year: 2024, month: 3, day: 1,  total: 300.0),
                new TestModel(year: 2024, month: 1, day: 20, total: 100.0),
                new TestModel(year: 2024, month: 2, day: 5,  total: 200.0),
            };

            var chart = testVm.TestGetChart(items);
            var points = chart.Series[0].ValuesOf<DateTimePoint>();

            Assert.Equal(100.0, points[0].Value);
            Assert.Equal(200.0, points[1].Value);
            Assert.Equal(300.0, points[2].Value);
        }

        [Fact]
        public void GetChart_PointsAreAtTheDateOfTheRow()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportDynamicRestModel>
            {
                new TestModel(year: 2024, month: 2, day: 5, total: 200.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new DateTime(2024, 2, 5), chart.Series[0].ValuesOf<DateTimePoint>()[0].DateTime);
        }

        [Fact]
        public void GetChart_YAxisIsPlainAxis()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportDynamicRestModel>());

            Assert.IsType<Axis>(chart.YAxes[0]);
        }

        [Fact]
        public void GetChart_WithData_LineSeriesHasExpectedPointCount()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportDynamicRestModel>
            {
                new TestModel(year: 2024, month: 1, day: 15, total: 1000.0),
                new TestModel(year: 2024, month: 2, day: 10, total: 1200.0),
                new TestModel(year: 2024, month: 3, day: 5,  total: 900.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(3, chart.Series[0].ValuesOf<DateTimePoint>().Length);
        }

        [Fact]
        public void GetSql_ContainsBaseQueryText()
        {
            var testVm = CreateTestableVM();

            var sql = testVm.TestGetSql();

            Assert.Contains("ReportDynamicRestVM", sql);
        }

        [Fact]
        public void GetSql_WithNoFilters_DoesNotContainAndClause()
        {
            var testVm = CreateTestableVM();

            var sql = testVm.TestGetSql();

            Assert.DoesNotContain(" and ", sql);
        }

        [Fact]
        public void GetSql_WithNoFilters_ReturnsNonEmptySql()
        {
            var testVm = CreateTestableVM();

            var sql = testVm.TestGetSql();

            Assert.NotEmpty(sql);
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

            this.dbMock.Verify(x => x.ExecuteQuery<ReportDynamicRestModel>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RefreshDataCommand_SetsChart_AfterRefresh()
        {
            Assert.Same(ReportChart.Empty, this.vm.Chart);

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.NotSame(ReportChart.Empty, this.vm.Chart);
            Assert.Single(this.vm.Chart.Series);
        }

        [Fact]
        public async Task RefreshDataCommand_WithData_PopulatesEntities()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportDynamicRestModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportDynamicRestModel>
                {
                    new TestModel(year: 2024, month: 1, day: 15, total: 1000.0),
                    new TestModel(year: 2024, month: 2, day: 10, total: 1200.0),
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

        private sealed class TestableVM : ReportDynamicRestVM
        {
            public TestableVM(IFinancistoDatabase db)
                : base(db)
            {
            }

            public ReportChart TestGetChart(List<ReportDynamicRestModel> list) =>
                GetChart(list);

            public string TestGetSql() => GetSql();
        }

        private sealed class TestModel : ReportDynamicRestModel
        {
            public TestModel(int year, int month, int day, double? total)
            {
                Year = year;
                Month = month;
                Day = day;
                Total = total;
            }
        }
    }
}
