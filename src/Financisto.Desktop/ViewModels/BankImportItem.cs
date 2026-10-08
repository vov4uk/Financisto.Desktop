using System.ComponentModel;
using Financisto.BankHelpers;
using Financisto.Common.Localization;
using Prism.Mvvm;

namespace Financisto.Desktop.ViewModels
{
    /// <summary>One bank helper in the Import menu.</summary>
    public sealed class BankImportItem : BindableBase
    {
        private string title;

        public BankImportItem(IBankHelper helper)
        {
            Helper = helper;
            title = helper.BankTitle;

            // The title may depend on the UI language (the menu is built once and lives as long as the app).
            LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        }

        public IBankHelper Helper { get; }

        public string Title
        {
            get => title;
            private set => SetProperty(ref title, value);
        }

        /// <summary>The statement format shown at the right of the menu entry, e.g. <c>CSV</c>.</summary>
        public string ReportTypeLabel => Helper.ReportType.ToString().ToUpperInvariant();

        public byte[]? Icon => Helper.Icon;

        private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalizationService.CurrentCulture))
            {
                Title = Helper.BankTitle;
            }
        }
    }
}
