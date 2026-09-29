namespace Financisto.Reports.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.DataAccess.Abstractions;
    using LiveChartsCore.SkiaSharpView;
    using Moq;
    using Xunit;

    public class ReportStructureActivesVMTests
    {
        private readonly Mock<IFinancistoDatabase> dbMock;
        private readonly Mock<IDialogService> dialogMock;
        private readonly ReportStructureActivesVM vm;

        public ReportStructureActivesVMTests()
        {
            this.dbMock = new Mock<IFinancistoDatabase>();
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureActivesModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureActivesModel>());

            DbManual.SetupTests(new List<CurrencyModel> { new CurrencyModel() });
            DbManual.SetupTests(new List<AccountFilterModel> { new AccountFilterModel() });
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel() });
            DbManual.SetupTests(new List<ProjectModel> { new ProjectModel() });
            DbManual.SetupTests(new List<PayeeModel> { new PayeeModel() });

            this.dialogMock = new Mock<IDialogService>();

            this.vm = new ReportStructureActivesVM(this.dbMock.Object);
            this.vm.DialogService = this.dialogMock.Object;
            this.vm.StartYearMonths = new YearMonths();
            this.vm.EndYearMonths = new YearMonths();
        }

        [Fact]
        public void Constructor_SetsDateFilter_ToNonNull()
        {
            Assert.NotNull(this.vm.DateFilter);
        }

        [Fact]
        public void GetChart_EmptyList_ReturnsNoSlices()
        {
            var testVm = CreateTestableVM();

            var chart = testVm.TestGetChart(new List<ReportStructureActivesModel>());

            Assert.Empty(chart.Series);
        }

        [Fact]
        public void GetChart_ExcludesAccountsNotIncludedInTotals()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureActivesModel>
            {
                new TestModel("Included",    includeInTotals: 1, balance: 1000.0),
                new TestModel("Excluded",    includeInTotals: 0, balance: 500.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Single(chart.Series);
            Assert.StartsWith("Included: ", chart.Series[0].Name);
        }

        [Fact]
        public void GetChart_ExcludesAccountsWithNonPositiveBalance()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureActivesModel>
            {
                new TestModel("Positive",  includeInTotals: 1, balance: 100.0),
                new TestModel("Zero",      includeInTotals: 1, balance: 0.0),
                new TestModel("Negative",  includeInTotals: 1, balance: -50.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Single(chart.Series);
            Assert.StartsWith("Positive: ", chart.Series[0].Name);
        }

        [Fact]
        public void GetChart_NoSmallSlices_DoesNotAddOthers()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureActivesModel>
            {
                new TestModel("A", includeInTotals: 1, balance: 500.0),
                new TestModel("B", includeInTotals: 1, balance: 500.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(2, chart.Series.Length);
            Assert.DoesNotContain(chart.Series, x => x.Name.StartsWith(LocalizationService.Instance.others + ": "));
        }

        [Fact]
        public void GetChart_ReturnsOnePieSeriesPerSlice_WithoutAxes()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureActivesModel>
            {
                new TestModel("A", includeInTotals: 1, balance: 500.0),
                new TestModel("B", includeInTotals: 1, balance: 500.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.All(chart.Series, x => Assert.IsType<PieSeries<double>>(x));
            Assert.Empty(chart.XAxes);
            Assert.Empty(chart.YAxes);
        }

        [Fact]
        public void GetChart_SliceIsNamedWithItsShareOfTheTotal()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureActivesModel>
            {
                new TestModel("A", includeInTotals: 1, balance: 750.0),
                new TestModel("B", includeInTotals: 1, balance: 250.0),
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal($"A: {0.75:P2}", chart.Series[0].Name);
            Assert.Equal($"B: {0.25:P2}", chart.Series[1].Name);
        }

        [Fact]
        public void GetChart_SmallSlices_GroupedIntoOthers()
        {
            var testVm = CreateTestableVM();
            var items = new List<ReportStructureActivesModel>
            {
                new TestModel("Big",   includeInTotals: 1, balance: 9990.0),
                new TestModel("Small", includeInTotals: 1, balance: 1.0),
            };

            var chart = testVm.TestGetChart(items);

            // "Big" slice + "others" slice (Small is < 1%)
            Assert.Equal(2, chart.Series.Length);
            Assert.Equal(new[] { 9990.0 }, chart.Series[0].ValuesOf<double>());
            Assert.StartsWith(LocalizationService.Instance.others + ": ", chart.Series[1].Name);
            Assert.Equal(new[] { 1.0 }, chart.Series[1].ValuesOf<double>());
        }

        [Fact]
        public void GetSql_WithDateFilter_ContainsUnixTimestamp()
        {
            var testVm = CreateTestableVM();
            var expectedTimestamp = new DateTimeOffset(testVm.DateFilter!.Value).ToUnixTimeMilliseconds().ToString();

            var sql = testVm.TestGetSql();

            Assert.Contains(expectedTimestamp, sql);
        }

        [Fact]
        public void GetSql_WithDateFilter_ReturnsNonEmptySql()
        {
            var testVm = CreateTestableVM();

            var sql = testVm.TestGetSql();

            Assert.NotEmpty(sql);
        }

        [Fact]
        public void GetSql_WithoutDateFilter_ReturnsEmptyAndShowsMessage()
        {
            var testVm = CreateTestableVM();
            testVm.DateFilter = null;

            var sql = testVm.TestGetSql();

            Assert.Empty(sql);
            this.dialogMock.Verify(x => x.ShowMessage(LocalizationService.Instance.please_select_date), Times.Once);
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

            this.dbMock.Verify(x => x.ExecuteQuery<ReportStructureActivesModel>(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RefreshDataCommand_SetsChart_AfterRefresh()
        {
            Assert.Same(ReportChart.Empty, this.vm.Chart);

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.NotSame(ReportChart.Empty, this.vm.Chart);
        }

        [Fact]
        public async Task RefreshDataCommand_WithoutDateFilter_DoesNotQuery()
        {
            this.vm.DateFilter = null;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            this.dbMock.Verify(x => x.ExecuteQuery<ReportStructureActivesModel>(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RefreshDataCommand_WithData_PopulatesEntities()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureActivesModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureActivesModel>
                {
                    new TestModel("Savings", includeInTotals: 1, balance: 500.0),
                    new TestModel("Wallet", includeInTotals: 1, balance: 200.0),
                });

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(2, this.vm.Entities.Count);
            Assert.Equal(2, this.vm.Chart.Series.Length);
        }

        private TestableVM CreateTestableVM()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.DialogService = this.dialogMock.Object;
            testVm.StartYearMonths = new YearMonths();
            testVm.EndYearMonths = new YearMonths();
            return testVm;
        }

        private sealed class TestableVM : ReportStructureActivesVM
        {
            public TestableVM(IFinancistoDatabase db)
                : base(db)
            {
            }

            public ReportChart TestGetChart(List<ReportStructureActivesModel> list) =>
                GetChart(list);

            public string TestGetSql() => GetSql();
        }

        private sealed class TestModel : ReportStructureActivesModel
        {
            public TestModel(string title, long includeInTotals, double? balance)
            {
                Title = title;
                AccountIsIncludeInTotals = includeInTotals;
                DefaultCurrencyBalance = balance;
            }
        }
    }
}
