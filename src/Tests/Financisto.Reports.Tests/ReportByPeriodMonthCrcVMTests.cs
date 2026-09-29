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

    public class ReportByPeriodMonthCrcVMTests
    {
        private readonly Mock<IFinancistoDatabase> dbMock;
        private readonly ReportByPeriodMonthCrcVM vm;

        public ReportByPeriodMonthCrcVMTests()
        {
            this.dbMock = new Mock<IFinancistoDatabase>();
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportByPeriodMonthCrcModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportByPeriodMonthCrcModel>());

            DbManual.SetupTests(new List<CurrencyModel> { new CurrencyModel() });
            DbManual.SetupTests(new List<AccountFilterModel> { new AccountFilterModel() });
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel() });
            DbManual.SetupTests(new List<ProjectModel> { new ProjectModel() });
            DbManual.SetupTests(new List<PayeeModel> { new PayeeModel() });

            this.vm = new ReportByPeriodMonthCrcVM(this.dbMock.Object);
            this.vm.StartYearMonths = new YearMonths();
            this.vm.EndYearMonths = new YearMonths();
        }

        [Fact]
        public void GetChart_FirstTwoSeriesAreColumnSeries()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportByPeriodMonthCrcModel>());

            Assert.IsType<ColumnSeries<double>>(chart.Series[0]);
            Assert.IsType<ColumnSeries<double>>(chart.Series[1]);
        }

        [Fact]
        public void GetChart_HasOneXAxisAndOneYAxis()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportByPeriodMonthCrcModel>());

            Assert.Single(chart.XAxes);
            Assert.Single(chart.YAxes);
        }

        [Fact]
        public void GetChart_HasThreeSeries()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportByPeriodMonthCrcModel>());

            Assert.Equal(3, chart.Series.Length);
        }

        [Fact]
        public void GetChart_SeriesAreNamedIncomeExpenseAndSaldo()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportByPeriodMonthCrcModel>());

            Assert.Equal(LocalizationService.Instance.income, chart.Series[0].Name);
            Assert.Equal(LocalizationService.Instance.expense, chart.Series[1].Name);
            Assert.Equal(LocalizationService.Instance.saldo, chart.Series[2].Name);
        }

        [Fact]
        public void GetChart_ThirdSeriesIsLineSeries()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportByPeriodMonthCrcModel>());

            Assert.IsType<LineSeries<double>>(chart.Series[2]);
        }

        [Fact]
        public void GetChart_WithData_IncomeSeriesHasExpectedValues()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportByPeriodMonthCrcModel>
            {
                new TestModel(year: 2024, month: 1, creditSum: 500.0, debitSum: 300.0, saldo: 200.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 500.0 }, chart.Series[0].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_WithData_ExpenseSeriesHasExpectedItemCount()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportByPeriodMonthCrcModel>
            {
                new TestModel(year: 2024, month: 1, creditSum: 500.0, debitSum: 300.0, saldo: 200.0),
                new TestModel(year: 2024, month: 2, creditSum: 400.0, debitSum: 250.0, saldo: 150.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(2, chart.Series[1].ValuesOf<double>().Length);
        }

        [Fact]
        public void GetChart_WithData_ExpenseSeriesHasExpectedValues()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportByPeriodMonthCrcModel>
            {
                new TestModel(year: 2024, month: 1, creditSum: 500.0, debitSum: 300.0, saldo: 200.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 300.0 }, chart.Series[1].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_WithData_SaldoSeriesHasExpectedPoints()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportByPeriodMonthCrcModel>
            {
                new TestModel(year: 2024, month: 1, creditSum: 500.0, debitSum: 300.0, saldo: 200.0),
                new TestModel(year: 2024, month: 2, creditSum: 400.0, debitSum: 250.0, saldo: 150.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 200.0, 150.0 }, chart.Series[2].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_WithData_NullSums_TreatedAsZero()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportByPeriodMonthCrcModel>
            {
                new TestModel(year: 2024, month: 1, creditSum: null, debitSum: null, saldo: null),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 0.0 }, chart.Series[0].ValuesOf<double>());
            Assert.Equal(new[] { 0.0 }, chart.Series[1].ValuesOf<double>());
            Assert.Equal(new[] { 0.0 }, chart.Series[2].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_WithData_XAxisLabelsAreYearAndMonth()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportByPeriodMonthCrcModel>
            {
                new TestModel(year: 2024, month: 1, creditSum: 500.0, debitSum: 300.0, saldo: 200.0),
                new TestModel(year: 2024, month: 12, creditSum: 400.0, debitSum: 250.0, saldo: 150.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { "2024-01", "2024-12" }, chart.XAxes[0].LabelsOf());
        }

        [Fact]
        public void GetSql_NoCurrencyFilter_ContainsZeroForCurrencyFlag()
        {
            var testVm = CreateTestableVM();

            var sql = testVm.TestGetSql();

            Assert.Contains("0 =", sql);
        }

        [Fact]
        public void GetSql_NoCurrencyFilter_DoesNotContainCurrencyIdClause()
        {
            var testVm = CreateTestableVM();

            var sql = testVm.TestGetSql();

            Assert.DoesNotContain("from_account_crc_id", sql);
        }

        [Fact]
        public void GetSql_ReturnsNonEmptySql()
        {
            var testVm = CreateTestableVM();

            var sql = testVm.TestGetSql();

            Assert.NotEmpty(sql);
        }

        [Fact]
        public void GetSql_WithCurrencyFilter_ContainsCurrencyIdClause()
        {
            var testVm = CreateTestableVM();
            testVm.CurentCurrency = new CurrencyModel { Id = 3 };

            var sql = testVm.TestGetSql();

            Assert.Contains("from_account_crc_id = 3", sql);
        }

        [Fact]
        public void GetSql_WithCurrencyFilter_ContainsOneForCurrencyFlag()
        {
            var testVm = CreateTestableVM();
            testVm.CurentCurrency = new CurrencyModel { Id = 3 };

            var sql = testVm.TestGetSql();

            Assert.Contains("1 =", sql);
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

            this.dbMock.Verify(x => x.ExecuteQuery<ReportByPeriodMonthCrcModel>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RefreshDataCommand_SetsChart_AfterRefresh()
        {
            Assert.Same(ReportChart.Empty, this.vm.Chart);

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.NotSame(ReportChart.Empty, this.vm.Chart);
            Assert.Equal(3, this.vm.Chart.Series.Length);
        }

        [Fact]
        public async Task RefreshDataCommand_WithData_PopulatesEntities()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportByPeriodMonthCrcModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportByPeriodMonthCrcModel>
                {
                    new TestModel(year: 2024, month: 1, creditSum: 500.0, debitSum: 300.0, saldo: 200.0),
                    new TestModel(year: 2024, month: 2, creditSum: 400.0, debitSum: 250.0, saldo: 150.0),
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

        private sealed class TestableVM : ReportByPeriodMonthCrcVM
        {
            public TestableVM(IFinancistoDatabase db)
                : base(db)
            {
            }

            public ReportChart TestGetChart(List<ReportByPeriodMonthCrcModel> list) =>
                GetChart(list);

            public string TestGetSql() => GetSql();
        }

        private sealed class TestModel : ReportByPeriodMonthCrcModel
        {
            public TestModel(long year, long month, double? creditSum, double? debitSum, double? saldo)
            {
                Year = year;
                Month = month;
                CreditSum = creditSum;
                DebitSum = debitSum;
                Saldo = saldo;
            }
        }
    }
}
