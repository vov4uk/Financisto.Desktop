using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Financisto.Common.Attribute;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Prism.Commands;
using Prism.Mvvm;

namespace Financisto.Reports
{
    /// <summary>The Reports page: a tree of available reports and a tab per opened one.</summary>
    public class ReportsControlVM : BindableBase
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IFinancistoDatabase financistoDatabase;
        private readonly IDialogService dialogService;

        public ReportsControlVM(IFinancistoDatabase financistoDatabase, IDialogService dialogService = null)
        {
            this.financistoDatabase = financistoDatabase;
            this.dialogService = dialogService;
            BuildReportsTree();
            LocalizationService.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(LocalizationService.CurrentCulture))
                {
                    BuildReportsTree();
                    UpdateHeaders();
                }
            };
        }

        private void BuildReportsTree()
        {
            TreeNode income_outcome = new TreeNode("reports_income_expense_for_period")
            {
                Child = new List<TreeNode>
                {
                    new TreeNode(typeof(ReportByPeriodMonthCrcVM)),
                }
            };

            TreeNode structure = new TreeNode("reports_structure")
            {
                Child = new List<TreeNode>
                {
                    new TreeNode(typeof(ReportStructureActivesVM)),
                    new TreeNode(typeof(ReportStructureIncomeExpenseVM)),
                    new TreeNode(typeof(ByCategoryReportVM)),
                    new TreeNode(typeof(ReportStructureSaldoVM)),
                }
            };

            TreeNode dynam = new TreeNode("reports_dynamics")
            {
                Child = new List<TreeNode>
                {
                    new TreeNode(typeof(ReportDynamicDebitCretitPayeeVM)),
                    new TreeNode(typeof(ReportDynamicRestVM))
                }
            };

            reportsInfo = new List<TreeNode>
            {
                income_outcome,
                structure,
                dynam
            };

            RaisePropertyChanged(nameof(ReportsInfo));
        }

        private DelegateCommand<IReportVM> _closeReportCommand;
        private DelegateCommand<string> _openReportCommand;
        private ObservableCollection<IReportVM> _reportsVM;
        private List<TreeNode> reportsInfo;
        private IReportVM _selectedReport;
        private TreeNode _selectedTreeNode;

        public ICommand CloseReportCommand => _closeReportCommand ??= new DelegateCommand<IReportVM>(CloseReport);

        public ICommand OpenReportCommand => _openReportCommand ??= new DelegateCommand<string>(OpenReport);

        public List<TreeNode> ReportsInfo
        {
            get
            {
                return reportsInfo;
            }
        }

        public TreeNode SelectedTreeNode
        {
            get => _selectedTreeNode;
            set
            {
                if (_selectedTreeNode == value)
                    return;
                _selectedTreeNode = value;
                if (_selectedTreeNode?.Type != null)
                {
                    OpenReport(_selectedTreeNode.Type);
                }
                RaisePropertyChanged(nameof(SelectedTreeNode));
            }
        }

        public ObservableCollection<IReportVM> ReportsVM
        {
            get => _reportsVM ??= new ObservableCollection<IReportVM>();
            set
            {
                if (_reportsVM == value)
                    return;
                _reportsVM = value;
                RaisePropertyChanged(nameof(ReportsVM));
            }
        }

        public IReportVM SelectedReport
        {
            get => _selectedReport;
            set
            {
                if (_selectedReport == value)
                    return;
                if (_selectedReport != null)
                    _selectedReport.IsSelected = false;
                _selectedReport = value;
                if (_selectedReport != null)
                    _selectedReport.IsSelected = true;
                Logger.Info($"Current report -> {_selectedReport?.GetType()?.Name}");
                RaisePropertyChanged(nameof(SelectedReport));
            }
        }

        public void CloseReport(IReportVM selected)
        {
            if (selected != null)
            {
                Logger.Info($"Close report -> {selected.GetType().Name}");
                var index = ReportsVM.IndexOf(selected);
                var wasSelected = SelectedReport == selected;
                ReportsVM.Remove(selected);
                if (wasSelected)
                {
                    // the report that took its place, else the last one
                    SelectedReport = ReportsVM.Count == 0 ? null : ReportsVM[Math.Min(index, ReportsVM.Count - 1)];
                }
            }
        }

        /// <summary>Opens a tab for the report (its full type name, as in <see cref="TreeNode.Type"/>) or selects it if it is already open.</summary>
        public void OpenReport(string reportType)
        {
            // a group node of the tree carries no type
            if (string.IsNullOrEmpty(reportType))
            {
                return;
            }

            IReportVM existingReport = ReportsVM.FirstOrDefault(p => p.GetType().ToString() == reportType);
            if (existingReport != null)
            {
                SelectedReport = existingReport;
            }
            else
            {
                Type type = Type.GetType(reportType, false, true);
                if (type != null && typeof(IReportVM).IsAssignableFrom(type))
                {
                    var newReport = (IReportVM)Activator.CreateInstance(type, financistoDatabase);
                    if (dialogService != null)
                    {
                        newReport.DialogService = dialogService;
                    }

                    newReport.Header = GetHeader(type);
                    ReportsVM.Add(newReport);
                    SelectedReport = newReport;
                }
            }
        }

        private static string GetHeader(Type reportType)
        {
            var header = (HeaderAttribute)System.Attribute.GetCustomAttribute(reportType, typeof(HeaderAttribute));
            return LocalizationService.Instance[header.Header];
        }

        private void UpdateHeaders()
        {
            foreach (var report in ReportsVM)
            {
                report.Header = GetHeader(report.GetType());
            }
        }
    }
}
