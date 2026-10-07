namespace Financisto.Reports.Tests
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Globalization;
    using System.Linq;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.DataAccess.Abstractions;
    using Moq;
    using Xunit;

    public class ReportsControlVMTests
    {
        private static readonly string ByMonths = typeof(ReportByPeriodMonthCrcVM).ToString();
        private static readonly string Actives = typeof(ReportStructureActivesVM).ToString();
        private static readonly string Saldo = typeof(ReportStructureSaldoVM).ToString();

        private readonly Mock<IFinancistoDatabase> dbMock = new Mock<IFinancistoDatabase>();
        private readonly Mock<IDialogService> dialogMock = new Mock<IDialogService>();
        private readonly ReportsControlVM vm;

        public ReportsControlVMTests()
        {
            this.vm = new ReportsControlVM(this.dbMock.Object, this.dialogMock.Object);
        }

        [Fact]
        public void ReportsInfo_HasThreeGroups()
        {
            Assert.Equal(3, this.vm.ReportsInfo.Count);
            Assert.All(this.vm.ReportsInfo, group => Assert.NotEmpty(group.Child));
        }

        [Fact]
        public void ReportsInfo_GroupsCarryNoType()
        {
            Assert.All(this.vm.ReportsInfo, group => Assert.Empty(group.Type));
        }

        [Fact]
        public void ReportsInfo_ListsEveryReport()
        {
            var types = this.vm.ReportsInfo.SelectMany(x => x.Child).Select(x => x.Type).ToList();

            Assert.Equal(7, types.Count);
            Assert.Contains(typeof(ReportByPeriodMonthCrcVM).ToString(), types);
            Assert.Contains(typeof(ReportStructureActivesVM).ToString(), types);
            Assert.Contains(typeof(ReportStructureIncomeExpenseVM).ToString(), types);
            Assert.Contains(typeof(ByCategoryReportVM).ToString(), types);
            Assert.Contains(typeof(ReportStructureSaldoVM).ToString(), types);
            Assert.Contains(typeof(ReportDynamicDebitCretitPayeeVM).ToString(), types);
            Assert.Contains(typeof(ReportDynamicRestVM).ToString(), types);
        }

        [Fact]
        public void ReportsInfo_NodesAreNamedAfterTheirHeader()
        {
            var node = this.vm.ReportsInfo.SelectMany(x => x.Child).First(x => x.Type == ByMonths);

            Assert.Equal(LocalizationService.Instance["reports_by_months"], node.Name);
        }

        [Fact]
        public void OpenReport_NewReport_IsAddedAndSelected()
        {
            this.vm.OpenReport(ByMonths);

            var report = Assert.Single(this.vm.ReportsVM);
            Assert.IsType<ReportByPeriodMonthCrcVM>(report);
            Assert.Same(report, this.vm.SelectedReport);
            Assert.True(report.IsSelected);
        }

        [Fact]
        public void OpenReport_NewReport_GetsTheLocalizedHeader()
        {
            this.vm.OpenReport(ByMonths);

            Assert.Equal(LocalizationService.Instance["reports_by_months"], this.vm.SelectedReport.Header);
        }

        [Fact]
        public void OpenReport_NewReport_GetsTheDialogService()
        {
            this.vm.OpenReport(ByMonths);

            Assert.Same(this.dialogMock.Object, this.vm.SelectedReport.DialogService);
        }

        [Fact]
        public void OpenReport_WithoutDialogService_KeepsTheReportsDefault()
        {
            var noDialogVm = new ReportsControlVM(this.dbMock.Object);

            noDialogVm.OpenReport(ByMonths);

            Assert.NotNull(noDialogVm.SelectedReport.DialogService);
            noDialogVm.SelectedReport.DialogService.ShowMessage("does not throw");
        }

        [Fact]
        public void OpenReport_EveryReportOfTheTree_CanBeOpened()
        {
            foreach (var node in this.vm.ReportsInfo.SelectMany(x => x.Child))
            {
                this.vm.OpenReport(node.Type);

                Assert.Equal(node.Type, this.vm.SelectedReport.GetType().ToString());
                Assert.Equal(node.Name, this.vm.SelectedReport.Header);
            }

            Assert.Equal(7, this.vm.ReportsVM.Count);
        }

        [Fact]
        public void OpenReport_AlreadyOpen_SelectsTheOpenOneInsteadOfAddingAnother()
        {
            this.vm.OpenReport(ByMonths);
            var first = this.vm.SelectedReport;
            this.vm.OpenReport(Actives);

            this.vm.OpenReport(ByMonths);

            Assert.Equal(2, this.vm.ReportsVM.Count);
            Assert.Same(first, this.vm.SelectedReport);
        }

        [Fact]
        public void OpenReport_SecondReport_MovesTheSelection()
        {
            this.vm.OpenReport(ByMonths);
            var first = this.vm.SelectedReport;

            this.vm.OpenReport(Actives);

            Assert.False(first.IsSelected);
            Assert.True(this.vm.SelectedReport.IsSelected);
            Assert.Single(this.vm.ReportsVM, x => x.IsSelected);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("Financisto.Reports.NoSuchReportVM")]
        [InlineData("System.String")]
        public void OpenReport_NotAReport_DoesNothing(string type)
        {
            this.vm.OpenReport(type);

            Assert.Empty(this.vm.ReportsVM);
            Assert.Null(this.vm.SelectedReport);
        }

        [Fact]
        public void OpenReportCommand_OpensTheReport()
        {
            this.vm.OpenReportCommand.Execute(Saldo);

            Assert.IsType<ReportStructureSaldoVM>(this.vm.SelectedReport);
        }

        [Fact]
        public void SelectedReport_Set_RaisesPropertyChanged()
        {
            this.vm.OpenReport(ByMonths);
            this.vm.OpenReport(Actives);
            var raised = new List<string>();
            (this.vm as INotifyPropertyChanged).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            this.vm.SelectedReport = this.vm.ReportsVM[0];

            Assert.Contains(nameof(ReportsControlVM.SelectedReport), raised);
        }

        [Fact]
        public void SelectedReport_SetToNull_ClearsTheSelectedFlag()
        {
            this.vm.OpenReport(ByMonths);
            var report = this.vm.SelectedReport;

            this.vm.SelectedReport = null;

            Assert.False(report.IsSelected);
        }

        [Fact]
        public void CloseReport_SelectedReport_SelectsTheOneThatTookItsPlace()
        {
            this.vm.OpenReport(ByMonths);
            this.vm.OpenReport(Actives);
            this.vm.OpenReport(Saldo);
            this.vm.SelectedReport = this.vm.ReportsVM[1];
            var next = this.vm.ReportsVM[2];

            this.vm.CloseReport(this.vm.SelectedReport);

            Assert.Equal(2, this.vm.ReportsVM.Count);
            Assert.Same(next, this.vm.SelectedReport);
            Assert.True(next.IsSelected);
        }

        [Fact]
        public void CloseReport_SelectedLastReport_SelectsThePreviousOne()
        {
            this.vm.OpenReport(ByMonths);
            this.vm.OpenReport(Actives);
            var previous = this.vm.ReportsVM[0];

            this.vm.CloseReport(this.vm.SelectedReport);

            Assert.Same(previous, this.vm.SelectedReport);
            Assert.True(previous.IsSelected);
        }

        [Fact]
        public void CloseReport_OnlyReport_SelectsNothing()
        {
            this.vm.OpenReport(ByMonths);
            var report = this.vm.SelectedReport;

            this.vm.CloseReport(report);

            Assert.Empty(this.vm.ReportsVM);
            Assert.Null(this.vm.SelectedReport);
            Assert.False(report.IsSelected);
        }

        [Fact]
        public void CloseReport_OtherReport_KeepsTheSelection()
        {
            this.vm.OpenReport(ByMonths);
            this.vm.OpenReport(Actives);
            var selected = this.vm.SelectedReport;

            this.vm.CloseReport(this.vm.ReportsVM[0]);

            Assert.Same(selected, this.vm.SelectedReport);
            Assert.True(selected.IsSelected);
        }

        [Fact]
        public void CloseReport_Null_DoesNothing()
        {
            this.vm.OpenReport(ByMonths);

            this.vm.CloseReport(null);

            Assert.Single(this.vm.ReportsVM);
        }

        [Fact]
        public void CloseReport_ClosedReportCanBeOpenedAgain()
        {
            this.vm.OpenReport(ByMonths);
            this.vm.CloseReport(this.vm.SelectedReport);

            this.vm.OpenReport(ByMonths);

            Assert.Single(this.vm.ReportsVM);
            Assert.IsType<ReportByPeriodMonthCrcVM>(this.vm.SelectedReport);
        }

        [Fact]
        public void CloseReportCommand_ClosesTheReport()
        {
            this.vm.OpenReport(ByMonths);
            var report = this.vm.SelectedReport;

            this.vm.CloseReportCommand.Execute(report);

            Assert.Empty(this.vm.ReportsVM);
        }

        [Fact]
        public void CultureChange_RebuildsTheTreeAndUpdatesTheHeadersOfOpenReports()
        {
            this.vm.OpenReport(ByMonths);
            var englishHeader = this.vm.SelectedReport.Header;
            var englishTree = this.vm.ReportsInfo;
            var treeChanged = false;
            (this.vm as INotifyPropertyChanged).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ReportsControlVM.ReportsInfo))
                {
                    treeChanged = true;
                }
            };

            var previous = LocalizationService.Instance.CurrentCulture;
            try
            {
                LocalizationService.Instance.CurrentCulture = CultureInfo.GetCultureInfo("uk");

                Assert.NotEqual(englishHeader, this.vm.SelectedReport.Header);
                Assert.Equal(LocalizationService.Instance["reports_by_months"], this.vm.SelectedReport.Header);
                Assert.True(treeChanged);
                Assert.NotSame(englishTree, this.vm.ReportsInfo);
            }
            finally
            {
                LocalizationService.Instance.CurrentCulture = previous;
            }
        }
    }
}
