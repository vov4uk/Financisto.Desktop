using System.ComponentModel;
using Financisto.Common;

namespace Financisto.Reports
{
    /// <summary>What <see cref="ReportsControlVM"/> needs from an open report tab.</summary>
    public interface IReportVM : INotifyPropertyChanged, IDataRefresh
    {
        /// <summary>Tab title.</summary>
        string Header { get; set; }

        IDialogService DialogService { get; set; }

        /// <summary>Whether this is the report shown in the page. All opened reports stay in the visual tree; only this one is visible.</summary>
        bool IsSelected { get; set; }
    }
}
