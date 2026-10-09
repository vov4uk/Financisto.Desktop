using Financisto.BankHelpers;

namespace Financisto.Desktop.ViewModels
{
    /// <summary>One bank helper in the Import menu (the menu is rebuilt when the UI language changes, so the title is read once).</summary>
    public sealed class BankImportItem
    {
        public BankImportItem(IBankHelper helper)
        {
            Helper = helper;
            Title = helper.BankTitle;
        }

        public IBankHelper Helper { get; }

        public string Title { get; }

        /// <summary>The statement format shown at the right of the menu entry, e.g. <c>CSV</c>.</summary>
        public string ReportTypeLabel => Helper.ReportType.ToString().ToUpperInvariant();

        public byte[]? Icon => Helper.Icon;
    }
}
