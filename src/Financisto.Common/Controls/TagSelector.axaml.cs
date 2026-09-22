using System;
using System.Collections.Generic;
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
    // SelectedTagsText mirrors SelectedTags as a comma-joined string for consumers (like the
    // free-text Transaction.Tags column) that store the selection as text rather than a list.
    [ExcludeFromCodeCoverage]
    public partial class TagSelector : UserControl
    {
        public static readonly StyledProperty<IList<TagModel>> SelectedTagsProperty =
            AvaloniaProperty.Register<TagSelector, IList<TagModel>>(
                nameof(SelectedTags),
                defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public static readonly StyledProperty<string> SelectedTagsTextProperty =
            AvaloniaProperty.Register<TagSelector, string>(
                nameof(SelectedTagsText),
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

        public IList<TagModel> SelectedTags
        {
            get => GetValue(SelectedTagsProperty);
            set => SetValue(SelectedTagsProperty, value);
        }

        public string SelectedTagsText
        {
            get => GetValue(SelectedTagsTextProperty);
            set => SetValue(SelectedTagsTextProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (_isSyncingFromItems)
            {
                return;
            }

            if (change.Property == SelectedTagsProperty)
            {
                var ids = ((IList<TagModel>)change.NewValue)?.Where(t => t?.Id != null).Select(t => t.Id);
                SyncItemsFromIds(ids);
            }
            else if (change.Property == SelectedTagsTextProperty)
            {
                var titles = SplitTitles((string)change.NewValue);
                var ids = _items
                    .Where(i => titles.Contains(i.Tag.Title, StringComparer.OrdinalIgnoreCase))
                    .Select(i => i.Tag.Id);
                SyncItemsFromIds(ids);
            }
        }

        private static List<string> SplitTitles(string text) =>
            (text ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

        private void SyncItemsFromIds(IEnumerable<int?> selectedIds)
        {
            var ids = (selectedIds ?? Enumerable.Empty<int?>()).ToHashSet();

            _isSyncingFromExternal = true;
            foreach (var item in _items)
            {
                item.IsSelected = ids.Contains(item.Tag.Id);
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

            var selected = _items.Where(i => i.IsSelected).Select(i => i.Tag).ToList();

            _isSyncingFromItems = true;
            SelectedTags = selected;
            SelectedTagsText = string.Join(", ", selected.Select(t => t.Title));
            _isSyncingFromItems = false;

            UpdateHeaderText();
        }

        private void UpdateHeaderText()
        {
            HeaderText.Text = string.Join(", ", _items.Where(i => i.IsSelected).Select(i => i.Tag.Title));
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
