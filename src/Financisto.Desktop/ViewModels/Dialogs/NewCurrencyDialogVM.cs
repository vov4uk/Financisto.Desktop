using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Financisto.Common.Entities;
using Financisto.Common.Localization;

namespace Financisto.Desktop.ViewModels.Dialogs
{
    public record CurrencyTemplateItem(bool IsNewCurrency, List<string> Template, string DisplayName);

    [ExcludeFromCodeCoverage]
    public class NewCurrencyDialogVM : DialogBaseVM
    {
        private CurrencyTemplateItem _selectedItem;

        public NewCurrencyDialogVM()
        {
            var items = DbManual.AllCurrencies.Select(t => new CurrencyTemplateItem(false, t, $"{t[0]} ({t[1]})")).ToList();
            items.Insert(0, new CurrencyTemplateItem(true, null, LocalizationService.Instance["new_currency"]));

            Items = items;
            SelectedItem = items[0];
        }

        public IReadOnlyList<CurrencyTemplateItem> Items { get; }

        public CurrencyTemplateItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged(nameof(SelectedItem));
            }
        }

        public override object OnRequestSave() => SelectedItem;
    }
}
