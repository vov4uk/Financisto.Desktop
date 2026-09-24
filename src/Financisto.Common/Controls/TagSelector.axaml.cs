using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Financisto.Common.Entities;
using Financisto.Common.Model;

namespace Financisto.Common.Controls
{
    // Avalonia's ComboBox has no built-in multi-select, so this opens a flyout of checkboxes instead.
    [ExcludeFromCodeCoverage]
    public partial class TagSelector : UserControl
    {
        public static readonly StyledProperty<ObservableCollection<TagModel>> SelectedTagsProperty =
            AvaloniaProperty.Register<TagSelector, ObservableCollection<TagModel>>(
                nameof(SelectedTags),
                defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        private readonly ObservableCollection<TagSelectionItem> _items = new();
        private bool _isSyncingFromExternal;
        private bool _isSyncingFromItems;

        public TagSelector()
        {
            InitializeComponent();

            foreach (var tag in DbManual.Tag.Where(t => t.Id.HasValue))
            {
                var item = new TagSelectionItem(tag);
                item.PropertyChanged += OnItemPropertyChanged;
                _items.Add(item);
            }

            TagsItemsControl.ItemsSource = _items;
            UpdateHeaderText();
        }

        public ObservableCollection<TagModel> SelectedTags
        {
            get => GetValue(SelectedTagsProperty);
            set => SetValue(SelectedTagsProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedTagsProperty && !_isSyncingFromItems)
            {
                SyncItemsFromSelectedTags(change.NewValue as ObservableCollection<TagModel>);
            }
        }

        private void SyncItemsFromSelectedTags(ObservableCollection<TagModel> selectedTags)
        {
            var selectedIds = (selectedTags ?? new ObservableCollection<TagModel>())
                .Where(t => t?.Id != null)
                .Select(t => t.Id)
                .ToHashSet();

            _isSyncingFromExternal = true;
            foreach (var item in _items)
            {
                item.IsSelected = selectedIds.Contains(item.Tag.Id);
            }
            _isSyncingFromExternal = false;

            UpdateHeaderText();
        }

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingFromExternal || e.PropertyName != nameof(TagSelectionItem.IsSelected))
            {
                return;
            }

            _isSyncingFromItems = true;
            SelectedTags = new ObservableCollection<TagModel>(_items.Where(i => i.IsSelected).Select(i => i.Tag));
            _isSyncingFromItems = false;

            UpdateHeaderText();
        }

        private void UpdateHeaderText()
        {
            HeaderText.Text = string.Join(" | ", _items.Where(i => i.IsSelected).Select(i => i.Tag.Title));
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items)
            {
                item.IsSelected = false;
            }
        }

        private sealed class TagSelectionItem : INotifyPropertyChanged
        {
            private bool _isSelected;

            public TagSelectionItem(TagModel tag) => Tag = tag;

            public TagModel Tag { get; }

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
