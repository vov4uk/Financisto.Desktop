namespace Financisto.Reports.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.DataAccess.Abstractions;
    using Moq;
    using Xunit;

    public class BaseReportVMTests
    {
        private readonly Mock<IFinancistoDatabase> dbMock;
        private readonly TestableVM vm;

        public BaseReportVMTests()
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

            this.vm = new TestableVM(this.dbMock.Object);
            this.vm.StartYearMonths = new YearMonths();
            this.vm.EndYearMonths = new YearMonths();
        }

        [Fact]
        public void Chart_BeforeTheFirstRefresh_IsEmpty()
        {
            Assert.Same(ReportChart.Empty, this.vm.Chart);
        }

        [Fact]
        public void Header_Set_RaisesPropertyChanged()
        {
            var raised = new List<string>();
            (this.vm as INotifyPropertyChanged).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            this.vm.Header = "By months";

            Assert.Equal("By months", this.vm.Header);
            Assert.Contains(nameof(IReportVM.Header), raised);
        }

        [Fact]
        public void IsSelected_Set_RaisesPropertyChangedOnlyOnChange()
        {
            var raised = new List<string>();
            (this.vm as INotifyPropertyChanged).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            this.vm.IsSelected = true;
            this.vm.IsSelected = true;

            Assert.True(this.vm.IsSelected);
            Assert.Single(raised, nameof(IReportVM.IsSelected));
        }

        [Fact]
        public void DialogService_ByDefault_CanShowAMessage()
        {
            Assert.NotNull(this.vm.DialogService);

            this.vm.DialogService.ShowMessage("logged, not shown");
        }

        [Fact]
        public async Task RefreshDataCommand_RaisesChartAndEntitiesPropertyChanged()
        {
            var raised = new List<string>();
            (this.vm as INotifyPropertyChanged).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.Contains(nameof(ReportDynamicRestVM.Chart), raised);
            Assert.Contains(nameof(ReportDynamicRestVM.Entities), raised);
        }

        [Fact]
        public async Task RefreshDataCommand_ReplacesTheChartEachTime()
        {
            await this.vm.RefreshDataCommand.ExecuteAsync();
            var first = this.vm.Chart;

            await this.vm.RefreshDataCommand.ExecuteAsync();

            Assert.NotSame(first, this.vm.Chart);
        }

        [Fact]
        public void Filters_ByDefault_AreTheAllEntryOfTheirLists()
        {
            var fresh = new TestableVM(this.dbMock.Object);

            Assert.Null(fresh.Project.Id);
            Assert.Null(fresh.Category.Id);
            Assert.Null(fresh.Account.Id);
            Assert.Null(fresh.Payee.Id);
            Assert.Null(fresh.CurentCurrency.Id);
        }

        [Fact]
        public void Filter_Set_RaisesPropertyChanged()
        {
            var raised = new List<string>();
            (this.vm as INotifyPropertyChanged).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            this.vm.Payee = new PayeeModel { Id = 1 };
            this.vm.Project = new ProjectModel { Id = 1 };
            this.vm.Category = new CategoryModel { Id = 1 };
            this.vm.TopCategory = new CategoryModel { Id = 1 };
            this.vm.Account = new AccountFilterModel { Id = 1 };
            this.vm.CurentCurrency = new CurrencyModel { Id = 1 };
            this.vm.StartYearMonths = new YearMonths { Year = 2024, Month = 1 };
            this.vm.EndYearMonths = new YearMonths { Year = 2024, Month = 2 };
            this.vm.DateFilter = new DateTime(2024, 1, 1);
            this.vm.From = new DateTime(2024, 1, 1);
            this.vm.To = new DateTime(2024, 2, 1);

            var expected = new HashSet<string>
            {
                nameof(TestableVM.Payee), nameof(TestableVM.Project), nameof(TestableVM.Category), nameof(TestableVM.TopCategory),
                nameof(TestableVM.Account), nameof(TestableVM.CurentCurrency), nameof(TestableVM.StartYearMonths),
                nameof(TestableVM.EndYearMonths), nameof(TestableVM.DateFilter), nameof(TestableVM.From), nameof(TestableVM.To),
            };
            Assert.True(expected.IsSubsetOf(raised), "missing: " + string.Join(", ", expected.Except(raised)));
        }

        [Fact]
        public void StandardFilter_NoFilters_IsEmpty()
        {
            Assert.Empty(this.vm.TestGetStandartTrnFilter());
        }

        [Fact]
        public void StandardFilter_StartMonth_LimitsFromThatMonth()
        {
            this.vm.StartYearMonths = new YearMonths { Year = 2024, Month = 3 };

            var filter = this.vm.TestGetStandartTrnFilter();

            Assert.Contains("(date_year = 2024 and date_month >= 3) or date_year > 2024", filter);
        }

        [Fact]
        public void StandardFilter_EndMonth_LimitsUpToThatMonth()
        {
            this.vm.EndYearMonths = new YearMonths { Year = 2024, Month = 9 };

            var filter = this.vm.TestGetStandartTrnFilter();

            Assert.Contains("(date_year = 2024 and date_month <= 9) or date_year < 2024", filter);
        }

        [Fact]
        public void StandardFilter_StartAndEndMonth_AreJoinedWithAnd()
        {
            this.vm.StartYearMonths = new YearMonths { Year = 2024, Month = 3 };
            this.vm.EndYearMonths = new YearMonths { Year = 2024, Month = 9 };

            var filter = this.vm.TestGetStandartTrnFilter();

            var startAt = filter.IndexOf("date_month >= 3", StringComparison.Ordinal);
            var andAt = filter.IndexOf(" and  ((date_year = 2024 and date_month <= 9)", StringComparison.Ordinal);
            Assert.True(startAt >= 0 && andAt > startAt, filter);
        }

        [Fact]
        public void StandardFilter_DateFilter_IsTheUnixTimeInMilliseconds()
        {
            var date = new DateTime(2024, 5, 17, 10, 30, 0, DateTimeKind.Local);
            this.vm.DateFilter = date;

            var filter = this.vm.TestGetStandartTrnFilter();

            Assert.Equal($" {new DateTimeOffset(date).ToUnixTimeMilliseconds()}", filter);
        }

        [Fact]
        public void StandardFilter_Payee_FiltersByPayeeId()
        {
            this.vm.Payee = new PayeeModel { Id = 5 };

            Assert.Contains("payee_id = 5", this.vm.TestGetStandartTrnFilter());
        }

        [Fact]
        public void StandardFilter_Project_FiltersByProjectId()
        {
            this.vm.Project = new ProjectModel { Id = 2 };

            Assert.Contains("project_id = 2", this.vm.TestGetStandartTrnFilter());
        }

        [Fact]
        public void StandardFilter_Account_FiltersByFromAccountId()
        {
            this.vm.Account = new AccountFilterModel { Id = 4 };

            Assert.Contains("from_account_id = 4", this.vm.TestGetStandartTrnFilter());
        }

        [Fact]
        public void StandardFilter_Category_FiltersByTheWholeSubtree()
        {
            this.vm.Category = new CategoryModel { Id = 7 };

            var filter = this.vm.TestGetStandartTrnFilter();

            Assert.Contains("category_id IN", filter);
            Assert.Contains("xxx._id = 7", filter);
            Assert.Contains("ctx.LEFT >= root.LEFT", filter);
        }

        [Fact]
        public void StandardFilter_SeveralFilters_AreJoinedWithAnd()
        {
            this.vm.Payee = new PayeeModel { Id = 5 };
            this.vm.Project = new ProjectModel { Id = 2 };
            this.vm.Account = new AccountFilterModel { Id = 4 };

            var filter = this.vm.TestGetStandartTrnFilter();

            Assert.Contains("payee_id = 5", filter);
            Assert.Contains("project_id = 2", filter);
            Assert.Contains("from_account_id = 4", filter);
            Assert.Equal(2, filter.Split(" and ").Length - 1);
        }

        private sealed class TestableVM : ReportDynamicRestVM
        {
            public TestableVM(IFinancistoDatabase db)
                : base(db)
            {
            }

            public string TestGetStandartTrnFilter() => GetStandartTrnFilter();
        }
    }
}
