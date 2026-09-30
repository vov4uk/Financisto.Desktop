using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Financisto.Common.Entities;
using Financisto.Common.Model;

namespace Financisto.Common.Filters
{
    // Avalonia's ComboBox has no built-in multi-select, so this opens a flyout of checkboxes instead.
    [ExcludeFromCodeCoverage]
    public partial class AccountsFilter : UserControl
    {
        public static readonly StyledProperty<ObservableCollection<AccountFilterModel>> SelectedAccountsProperty =
            AvaloniaProperty.Register<AccountsFilter, ObservableCollection<AccountFilterModel>>(
                nameof(SelectedAccounts),
                defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        private readonly ObservableCollection<AccountSelectionItem> _items = new();
        private bool _isSyncingFromExternal;
        private bool _isSyncingFromItems;

        public AccountsFilter()
        {
            InitializeComponent();

            foreach (var account in DbManual.SelectableAccounts)
            {
                var item = new AccountSelectionItem(account);
                item.PropertyChanged += OnItemPropertyChanged;
                _items.Add(item);
            }

            AccountsItemsControl.ItemsSource = _items;
            UpdateHeaderText();
        }

        public ObservableCollection<AccountFilterModel> SelectedAccounts
        {
            get => GetValue(SelectedAccountsProperty);
            set => SetValue(SelectedAccountsProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedAccountsProperty && !_isSyncingFromItems)
            {
                SyncItemsFromSelectedAccounts(change.NewValue as ObservableCollection<AccountFilterModel>);
            }
        }

        private void SyncItemsFromSelectedAccounts(ObservableCollection<AccountFilterModel> selectedAccounts)
        {
            var selectedIds = (selectedAccounts ?? new ObservableCollection<AccountFilterModel>())
                .Where(a => a?.Id != null)
                .Select(a => a.Id)
                .ToHashSet();

            _isSyncingFromExternal = true;
            foreach (var item in _items)
            {
                item.IsSelected = selectedIds.Contains(item.Account.Id);
            }
            _isSyncingFromExternal = false;

            UpdateHeaderText();
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromExternal || e.PropertyName != nameof(AccountSelectionItem.IsSelected))
            {
                return;
            }

            _isSyncingFromItems = true;
            SelectedAccounts = new ObservableCollection<AccountFilterModel>(_items.Where(i => i.IsSelected).Select(i => i.Account));
            _isSyncingFromItems = false;

            UpdateHeaderText();
        }

        private void UpdateHeaderText()
        {
            var text = string.Join(" | ", _items.Where(i => i.IsSelected).Select(i => i.Account.Title));
            HeaderText.Text = text;
            ToolTip.SetTip(HeaderButton, string.IsNullOrEmpty(text) ? null : text);
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items)
            {
                item.IsSelected = false;
            }
        }

        private sealed class AccountSelectionItem : INotifyPropertyChanged
        {
            private bool _isSelected;

            public AccountSelectionItem(AccountFilterModel account) => Account = account;

            public AccountFilterModel Account { get; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value)
                    {
                        return;
                    }

                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}
