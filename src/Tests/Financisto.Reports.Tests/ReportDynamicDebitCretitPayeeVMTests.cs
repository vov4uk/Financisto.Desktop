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

    public class ReportDynamicDebitCretitPayeeVMTests
    {
        private readonly Mock<IFinancistoDatabase> dbMock;
        private readonly Mock<IDialogService> dialogMock;
        private readonly ReportDynamicDebitCretitPayeeVM vm;

        public ReportDynamicDebitCretitPayeeVMTests()
        {
            this.dbMock = new Mock<IFinancistoDatabase>();
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportDynamicDebitCretitPayeeModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportDynamicDebitCretitPayeeModel>());

            DbManual.SetupTests(new List<CurrencyModel> { new CurrencyModel() });
            DbManual.SetupTests(new List<AccountFilterModel> { new AccountFilterModel() });
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel() });
            DbManual.SetupTests(new List<ProjectModel> { new ProjectModel() });
            DbManual.SetupTests(new List<PayeeModel> { new PayeeModel() });

            this.dialogMock = new Mock<IDialogService>();

            this.vm = new ReportDynamicDebitCretitPayeeVM(this.dbMock.Object);
            this.vm.DialogService = this.dialogMock.Object;
            this.vm.StartYearMonths = new YearMonths();
            this.vm.EndYearMonths = new YearMonths();

            // Ensure a Category is selected so GetSql() does not reach the dialog path
            this.vm.Category = new CategoryModel { Id = 1 };
        }

        [Fact]
        public void GetChart_XAxisIsDateTimeAxis()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportDynamicDebitCretitPayeeModel>());

            Assert.IsType<DateTimeAxis>(chart.XAxes[0]);
        }

        [Fact]
        public void GetChart_HasOneLineSeries()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportDynamicDebitCretitPayeeModel>());

            Assert.Single(chart.Series);
            Assert.IsType<LineSeries<DateTimePoint>>(chart.Series[0]);
        }

        [Fact]
        public void GetChart_HasOneXAxisAndOneYAxis()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportDynamicDebitCretitPayeeModel>());

            Assert.Single(chart.XAxes);
            Assert.Single(chart.YAxes);
        }

        [Fact]
        public void GetChart_NullTotal_TreatedAsZero()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportDynamicDebitCretitPayeeModel>
            {
                new TestModel(year: 2024, month: 1, total: null),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(0.0, chart.Series[0].ValuesOf<DateTimePoint>()[0].Value);
        }

        [Fact]
        public void GetChart_OrdersPointsByYearThenMonth()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportDynamicDebitCretitPayeeModel>
            {
                new TestModel(year: 2024, month: 3, total: -300.0),
                new TestModel(year: 2024, month: 1, total: -100.0),
                new TestModel(year: 2024, month: 2, total: -200.0),
            };

            var chart = testVm.TestGetChart(items);
            var points = chart.Series[0].ValuesOf<DateTimePoint>();

            Assert.Equal(-100.0, points[0].Value);
            Assert.Equal(-200.0, points[1].Value);
            Assert.Equal(-300.0, points[2].Value);
        }

        [Fact]
        public void GetChart_PointsAreAtTheFirstOfTheMonth()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportDynamicDebitCretitPayeeModel>
            {
                new TestModel(year: 2024, month: 2, total: -200.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new DateTime(2024, 2, 1), chart.Series[0].ValuesOf<DateTimePoint>()[0].DateTime);
        }

        [Fact]
        public void GetChart_YAxisIsPlainAxis()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportDynamicDebitCretitPayeeModel>());

            Assert.IsType<Axis>(chart.YAxes[0]);
        }

        [Fact]
        public void GetChart_WithData_LineSeriesHasExpectedPointCount()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportDynamicDebitCretitPayeeModel>
            {
                new TestModel(year: 2024, month: 1, total: -200.0),
                new TestModel(year: 2024, month: 2, total: -150.0),
                new TestModel(year: 2024, month: 3, total: -100.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(3, chart.Series[0].ValuesOf<DateTimePoint>().Length);
        }

        [Fact]
        public void GetSql_NeitherPayeeNorCategory_ReturnsEmpty()
        {
            var testVm = CreateTestableVM();

            // Leave both Payee.Id and Category.Id as null
            var sql = testVm.TestGetSql();

            Assert.Empty(sql);
            this.dialogMock.Verify(x => x.ShowMessage(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void GetSql_NoCurrencySelected_ContainsZeroForCurrencyFlag()
        {
            var testVm = CreateTestableVM();
            testVm.Category = new CategoryModel { Id = 1 };

            var sql = testVm.TestGetSql();

            Assert.Contains("0 =", sql);
        }

        [Fact]
        public void GetSql_NoCurrencySelected_DoesNotContainCurrencyIdClause()
        {
            var testVm = CreateTestableVM();
            testVm.Category = new CategoryModel { Id = 1 };

            var sql = testVm.TestGetSql();

            Assert.DoesNotContain("from_account_crc_id", sql);
        }

        [Fact]
        public void GetSql_WithCategorySelected_ReturnsNonEmptySql()
        {
            var testVm = CreateTestableVM();
            testVm.Category = new CategoryModel { Id = 1 };

            var sql = testVm.TestGetSql();

            Assert.NotEmpty(sql);
        }

        [Fact]
        public void GetSql_WithCurrencySelected_ContainsCurrencyIdClause()
        {
            var testVm = CreateTestableVM();
            testVm.Category = new CategoryModel { Id = 1 };
            testVm.CurentCurrency = new CurrencyModel { Id = 7 };

            var sql = testVm.TestGetSql();

            Assert.Contains("from_account_crc_id = 7", sql);
        }

        [Fact]
        public void GetSql_WithCurrencySelected_ContainsOneForCurrencyFlag()
        {
            var testVm = CreateTestableVM();
            testVm.Category = new CategoryModel { Id = 1 };
            testVm.CurentCurrency = new CurrencyModel { Id = 7 };

            var sql = testVm.TestGetSql();

            Assert.Contains("1 =", sql);
        }

        [Fact]
        public void GetSql_WithPayeeSelected_ReturnsNonEmptySql()
        {
            var testVm = CreateTestableVM();
            testVm.Payee = new PayeeModel { Id = 5 };

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

            this.dbMock.Verify(x => x.ExecuteQuery<ReportDynamicDebitCretitPayeeModel>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RefreshDataCommand_NeitherPayeeNorCategory_ShowsMessageAndKeepsTheChart()
        {
            this.vm.Category = new CategoryModel();

            await this.vm.RefreshDataCommand.ExecuteAsync();

            this.dialogMock.Verify(x => x.ShowMessage(It.IsAny<string>()), Times.Once);
            this.dbMock.Verify(x => x.ExecuteQuery<ReportDynamicDebitCretitPayeeModel>(It.IsAny<string>()), Times.Never);
            Assert.Same(ReportChart.Empty, this.vm.Chart);
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
                .Setup(x => x.ExecuteQuery<ReportDynamicDebitCretitPayeeModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportDynamicDebitCretitPayeeModel>
                {
                    new TestModel(year: 2024, month: 1, total: -200.0),
                    new TestModel(year: 2024, month: 2, total: -150.0),
                });

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(2, this.vm.Entities.Count);
        }

        private TestableVM CreateTestableVM()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.DialogService = this.dialogMock.Object;
            testVm.StartYearMonths = new YearMonths();
            testVm.EndYearMonths = new YearMonths();
            return testVm;
        }

        private sealed class TestableVM : ReportDynamicDebitCretitPayeeVM
        {
            public TestableVM(IFinancistoDatabase db)
                : base(db)
            {
            }

            public ReportChart TestGetChart(List<ReportDynamicDebitCretitPayeeModel> list) =>
                GetChart(list);

            public string TestGetSql() => GetSql();
        }

        private sealed class TestModel : ReportDynamicDebitCretitPayeeModel
        {
            public TestModel(int year, int month, double? total)
            {
                Year = year;
                Month = month;
                Total = total;
            }
        }
    }
}
