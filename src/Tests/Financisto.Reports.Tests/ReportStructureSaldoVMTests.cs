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

    public class ReportStructureSaldoVMTests
    {
        private readonly Mock<IFinancistoDatabase> dbMock;
        private readonly ReportStructureSaldoVM vm;

        public ReportStructureSaldoVMTests()
        {
            this.dbMock = new Mock<IFinancistoDatabase>();
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>());

            DbManual.SetupTests(new List<CurrencyModel> { new CurrencyModel() });
            DbManual.SetupTests(new List<AccountFilterModel> { new AccountFilterModel() });
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel() });
            DbManual.SetupTests(new List<ProjectModel> { new ProjectModel() });
            DbManual.SetupTests(new List<PayeeModel> { new PayeeModel() });

            this.vm = new ReportStructureSaldoVM(this.dbMock.Object);
        }

        [Fact]
        public void Constructor_SetsDefaultIsUsdCurrencySelected_ToTrue()
        {
            Assert.True(this.vm.IsUsdCurrencySelected);
        }

        [Fact]
        public void Constructor_SetsDefaultRange_ToLast6Months()
        {
            Assert.Equal(ReportStructureSaldoRange.Last6Months, this.vm.Range);
        }

        [Fact]
        public void GetChart_IsUsdCurrencySelected_UsesUsdBalance_ForAssets()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.IsUsdCurrencySelected = true;
            var items = new List<ReportStructureSaldoModel>
            {
                new ReportStructureSaldoModel { AssetsUSDBalance = 100, AssetsDefaultCurrencyBalance = 200, Date = DateOnly.FromDateTime(DateTime.Today) },
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 100.0 }, chart.Series[0].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_IsUsdCurrencySelected_UsesUsdBalance_ForLiabilities()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.IsUsdCurrencySelected = true;
            var items = new List<ReportStructureSaldoModel>
            {
                new ReportStructureSaldoModel { LiabilitiesUSDBalance = -100, LiabilitiesDefaultCurrencyBalance = -200, Date = DateOnly.FromDateTime(DateTime.Today) },
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { -100.0 }, chart.Series[1].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_IsUsdCurrencySelected_UsesUsdBalance_ForNetWorth()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.IsUsdCurrencySelected = true;
            var items = new List<ReportStructureSaldoModel>
            {
                new ReportStructureSaldoModel { NetWorthUSDBalance = 300, NetWorthDefaultCurrencyBalance = 800, Date = DateOnly.FromDateTime(DateTime.Today) },
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 300.0 }, chart.Series[2].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_NotUsdCurrencySelected_UsesDefaultCurrencyBalance_ForAssets()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.IsUsdCurrencySelected = false;
            var items = new List<ReportStructureSaldoModel>
            {
                new ReportStructureSaldoModel { AssetsUSDBalance = 100, AssetsDefaultCurrencyBalance = 200, DefaultCurrencySymbol = "UAH", Date = DateOnly.FromDateTime(DateTime.Today) },
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 200.0 }, chart.Series[0].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_NotUsdCurrencySelected_UsesDefaultCurrencyBalance_ForNetWorth()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.IsUsdCurrencySelected = false;
            var items = new List<ReportStructureSaldoModel>
            {
                new ReportStructureSaldoModel { NetWorthUSDBalance = 300, NetWorthDefaultCurrencyBalance = 800, DefaultCurrencySymbol = "UAH", Date = DateOnly.FromDateTime(DateTime.Today) },
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 800.0 }, chart.Series[2].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_ReturnsThreeSeries()
        {
            var testVm = new TestableVM(this.dbMock.Object);

            var chart = testVm.TestGetChart(new List<ReportStructureSaldoModel>());

            Assert.Equal(3, chart.Series.Length);
        }

        [Fact]
        public void GetChart_ReturnsOneXAxisAndOneYAxis()
        {
            var testVm = new TestableVM(this.dbMock.Object);

            var chart = testVm.TestGetChart(new List<ReportStructureSaldoModel>());

            Assert.Single(chart.XAxes);
            Assert.Single(chart.YAxes);
        }

        [Fact]
        public void GetChart_SortsItemsByDateAscending()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.IsUsdCurrencySelected = true;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var items = new List<ReportStructureSaldoModel>
            {
                new ReportStructureSaldoModel { NetWorthUSDBalance = 200, Date = today },
                new ReportStructureSaldoModel { NetWorthUSDBalance = 100, Date = today.AddMonths(-1) },
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { 100.0, 200.0 }, chart.Series[2].ValuesOf<double>());
        }

        [Fact]
        public void GetChart_SeriesAreAssetsAndLiabilitiesColumnsAndANetWorthLine()
        {
            var testVm = new TestableVM(this.dbMock.Object);

            var chart = testVm.TestGetChart(new List<ReportStructureSaldoModel>());

            Assert.IsType<ColumnSeries<double>>(chart.Series[0]);
            Assert.IsType<ColumnSeries<double>>(chart.Series[1]);
            Assert.IsType<LineSeries<double>>(chart.Series[2]);
            Assert.Equal(LocalizationService.Instance.assets, chart.Series[0].Name);
            Assert.Equal(LocalizationService.Instance.liabilities, chart.Series[1].Name);
            Assert.Equal(LocalizationService.Instance.net_worth, chart.Series[2].Name);
        }

        [Fact]
        public void GetChart_XAxisLabelsAreYearAndMonthInDateOrder()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            var items = new List<ReportStructureSaldoModel>
            {
                new ReportStructureSaldoModel { Date = new DateOnly(2024, 3, 31) },
                new ReportStructureSaldoModel { Date = new DateOnly(2024, 1, 31) },
                new ReportStructureSaldoModel { Date = new DateOnly(2024, 2, 29) },
            };

            var chart = testVm.TestGetChart(items);

            Assert.Equal(new[] { "2024-01", "2024-02", "2024-03" }, chart.XAxes[0].LabelsOf());
        }

        [Fact]
        public void GetChart_IsUsdCurrencySelected_YAxisLabelsHaveDollarSign()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.IsUsdCurrencySelected = true;

            var chart = testVm.TestGetChart(new List<ReportStructureSaldoModel>());

            Assert.EndsWith("$", chart.YAxes[0].Labeler(1000));
        }

        [Fact]
        public void GetChart_NotUsdCurrencySelected_YAxisLabelsHaveHomeCurrencySymbol()
        {
            var testVm = new TestableVM(this.dbMock.Object);
            testVm.IsUsdCurrencySelected = false;
            var items = new List<ReportStructureSaldoModel>
            {
                new ReportStructureSaldoModel { DefaultCurrencySymbol = "UAH", Date = DateOnly.FromDateTime(DateTime.Today) },
            };

            var chart = testVm.TestGetChart(items);

            Assert.EndsWith("UAH", chart.YAxes[0].Labeler(1000));
        }

        [Fact]
        public void IsUsdCurrencySelected_SetNewValue_RaisesPropertyChanged()
        {
            var raised = false;
            this.vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ReportStructureSaldoVM.IsUsdCurrencySelected))
                {
                    raised = true;
                }
            };

            this.vm.IsUsdCurrencySelected = false;

            Assert.True(raised);
        }

        [Fact]
        public void IsUsdCurrencySelected_SetSameValue_DoesNotRaisePropertyChanged()
        {
            var raised = false;
            this.vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ReportStructureSaldoVM.IsUsdCurrencySelected))
                {
                    raised = true;
                }
            };

            this.vm.IsUsdCurrencySelected = true;

            Assert.False(raised);
        }

        [Fact]
        public void Range_SetNewValue_RaisesPropertyChanged()
        {
            var raised = false;
            this.vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ReportStructureSaldoVM.Range))
                {
                    raised = true;
                }
            };

            this.vm.Range = ReportStructureSaldoRange.Last12Months;

            Assert.True(raised);
        }

        [Fact]
        public void Range_SetSameValue_DoesNotRaisePropertyChanged()
        {
            var raised = false;
            this.vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ReportStructureSaldoVM.Range))
                {
                    raised = true;
                }
            };

            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            Assert.False(raised);
        }

        [Fact]
        public async Task RefreshDataCommand_AccountExcludedFromTotals_NotIncludedInBalance()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "BANK", defaultCurrencyBalance: 100.0, usdBalance: 50.0),
                    new TestRawModel(includeInTotals: false, accountType: "BANK", defaultCurrencyBalance: 9999.0, usdBalance: 9999.0),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(100, this.vm.Entities[0].AssetsDefaultCurrencyBalance);
            Assert.Equal(50, this.vm.Entities[0].AssetsUSDBalance);
        }

        [Fact]
        public async Task RefreshDataCommand_CalculatesNetWorth_AsAssetsPlusLiabilities()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "CASH", defaultCurrencyBalance: 1000.0, usdBalance: 400.0),
                    new TestRawModel(includeInTotals: true, accountType: "LIABILITY", defaultCurrencyBalance: -200.0, usdBalance: -80.0),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(800, this.vm.Entities[0].NetWorthDefaultCurrencyBalance);
            Assert.Equal(320, this.vm.Entities[0].NetWorthUSDBalance);
        }

        [Fact]
        public async Task RefreshDataCommand_AccountWithoutUsdRate_KeepsTheOthersInTheUsdTotal()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "CASH", defaultCurrencyBalance: 100.0, usdBalance: 50.0),
                    new TestRawModel(includeInTotals: true, accountType: "BANK", defaultCurrencyBalance: 200.0, usdBalance: null),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(50, this.vm.Entities[0].AssetsUSDBalance);
            Assert.Equal(300, this.vm.Entities[0].AssetsDefaultCurrencyBalance);
            Assert.Equal(50, this.vm.Entities[0].NetWorthUSDBalance);
        }

        [Fact]
        public async Task RefreshDataCommand_AccountWithoutHomeCurrencyRate_KeepsTheOthersInTheHomeCurrencyTotal()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "CASH", defaultCurrencyBalance: 100.0, usdBalance: 50.0),
                    new TestRawModel(includeInTotals: true, accountType: "BANK", defaultCurrencyBalance: null, usdBalance: 80.0),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(100, this.vm.Entities[0].AssetsDefaultCurrencyBalance);
            Assert.Equal(130, this.vm.Entities[0].AssetsUSDBalance);
        }

        [Fact]
        public async Task RefreshDataCommand_LiabilityWithoutRate_KeepsTheOtherLiabilities()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "LIABILITY", defaultCurrencyBalance: -500.0, usdBalance: -200.0),
                    new TestRawModel(includeInTotals: true, accountType: "LIABILITY", defaultCurrencyBalance: null, usdBalance: null),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(-500, this.vm.Entities[0].LiabilitiesDefaultCurrencyBalance);
            Assert.Equal(-200, this.vm.Entities[0].LiabilitiesUSDBalance);
        }

        [Fact]
        public async Task RefreshDataCommand_NoAccountCanBeConverted_TotalsAreZeroButTheMonthIsKept()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "CASH", defaultCurrencyBalance: null, usdBalance: null),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(6, this.vm.Entities.Count);
            Assert.All(this.vm.Entities, x => Assert.Equal(0, x.NetWorthDefaultCurrencyBalance));
        }

        [Fact]
        public async Task RefreshDataCommand_ConvertsAtTheDateOfEachMonth()
        {
            var queries = new List<string>();
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .Callback<string>(queries.Add)
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>());
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            // the rates are looked up as of the date the balances are for, not as of today
            var lastDayOfCurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(1).AddDays(-1);
            var expected = new DateTimeOffset(lastDayOfCurrentMonth).ToUnixTimeMilliseconds();
            var at = queries[0].IndexOf("rate_date <=", StringComparison.Ordinal);
            Assert.True(at >= 0, "no rate lookup in the query");
            Assert.Equal(expected.ToString(), queries[0].Substring(at + "rate_date <=".Length).TrimStart().Split(' ', ')')[0]);
            Assert.DoesNotContain("{0}", queries[0]);
        }

        [Fact]
        public async Task RefreshDataCommand_CurrentYear_ExecutesQueryForEachMonthInCurrentYear()
        {
            this.vm.Range = ReportStructureSaldoRange.CurrentYear;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            this.dbMock.Verify(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()), Times.Exactly(DateTime.Today.Month));
        }

        [Fact]
        public async Task RefreshDataCommand_EmptyQueryResult_EntitiesIsEmpty()
        {
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Empty(this.vm.Entities);
        }

        [Fact]
        public async Task RefreshDataCommand_Last12Months_ExecutesQueryTwelveTimes()
        {
            this.vm.Range = ReportStructureSaldoRange.Last12Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            this.dbMock.Verify(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()), Times.Exactly(12));
        }

        [Fact]
        public async Task RefreshDataCommand_AllPeriods_ExecutesQueryOncePerMonthSinceFirstTransaction()
        {
            var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 15).AddMonths(-4);
            var raw = new FirstTransactionRawModel();
            typeof(FirstTransactionRawModel).GetProperty(nameof(FirstTransactionRawModel.FirstDateTime))!
                .SetValue(raw, new DateTimeOffset(firstMonth).ToUnixTimeMilliseconds());
            this.dbMock
                .Setup(x => x.ExecuteQuery<FirstTransactionRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<FirstTransactionRawModel> { raw });
            this.vm.Range = ReportStructureSaldoRange.AllPeriods;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            // first transaction month + 4 following months, ending with the current one
            this.dbMock.Verify(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()), Times.Exactly(5));
        }

        [Fact]
        public async Task RefreshDataCommand_Last24Months_ExecutesQueryTwentyFourTimes()
        {
            this.vm.Range = ReportStructureSaldoRange.Last24Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            this.dbMock.Verify(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()), Times.Exactly(24));
        }

        [Fact]
        public async Task RefreshDataCommand_Last2Years_ExecutesQueryFromCurrentMonthToJanuaryOfPreviousYear()
        {
            this.vm.Range = ReportStructureSaldoRange.Last2Years;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            var expectedCount = DateTime.Today.Month + 12;
            this.dbMock.Verify(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()), Times.Exactly(expectedCount));
        }

        [Fact]
        public async Task RefreshDataCommand_Last6Months_ExecutesQuerySixTimes()
        {
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            this.dbMock.Verify(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()), Times.Exactly(6));
        }

        [Fact]
        public async Task RefreshDataCommand_SetsDefaultCurrencySymbol_FromFirstRawRow()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "CASH", defaultCurrencyBalance: 1.0, usdBalance: 1.0, defaultCurrencySymbol: "UAH"),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal("UAH", this.vm.Entities[0].DefaultCurrencySymbol);
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
        public async Task RefreshDataCommand_WithAssetRows_SumsAssetsBalances()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "CASH", defaultCurrencyBalance: 100.0, usdBalance: 50.0, defaultCurrencySymbol: "UAH"),
                    new TestRawModel(includeInTotals: true, accountType: "BANK", defaultCurrencyBalance: 200.0, usdBalance: 80.0, defaultCurrencySymbol: "UAH"),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(300, this.vm.Entities[0].AssetsDefaultCurrencyBalance);
            Assert.Equal(130, this.vm.Entities[0].AssetsUSDBalance);
        }

        [Fact]
        public async Task RefreshDataCommand_WithData_EntityCountMatchesDateRange()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "CASH", defaultCurrencyBalance: 100.0, usdBalance: 50.0),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(6, this.vm.Entities.Count);
        }

        [Fact]
        public async Task RefreshDataCommand_WithLiabilityRows_SumsLiabilitiesBalances()
        {
            this.dbMock
                .Setup(x => x.ExecuteQuery<ReportStructureSaldoRawModel>(It.IsAny<string>()))
                .ReturnsAsync(new List<ReportStructureSaldoRawModel>
                {
                    new TestRawModel(includeInTotals: true, accountType: "LIABILITY", defaultCurrencyBalance: -500.0, usdBalance: -200.0),
                    new TestRawModel(includeInTotals: true, accountType: "LIABILITY", defaultCurrencyBalance: -300.0, usdBalance: -100.0),
                });
            this.vm.Range = ReportStructureSaldoRange.Last6Months;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Equal(-800, this.vm.Entities[0].LiabilitiesDefaultCurrencyBalance);
            Assert.Equal(-300, this.vm.Entities[0].LiabilitiesUSDBalance);
        }

        private sealed class TestableVM : ReportStructureSaldoVM
        {
            public TestableVM(IFinancistoDatabase db)
                : base(db)
            {
            }

            public ReportChart TestGetChart(List<ReportStructureSaldoModel> list) =>
                GetChart(list);
        }

        private sealed class TestRawModel : ReportStructureSaldoRawModel
        {
            public TestRawModel(
                bool includeInTotals,
                string accountType,
                double? defaultCurrencyBalance = null,
                double? usdBalance = null,
                string defaultCurrencySymbol = null)
            {
                AccountIsIncludeInTotals = includeInTotals;
                AccountType = accountType;
                DefaultCurrencyBalance = defaultCurrencyBalance;
                USDBalance = usdBalance;
                DefaultCurrencySymbol = defaultCurrencySymbol;
            }
        }
    }
}
