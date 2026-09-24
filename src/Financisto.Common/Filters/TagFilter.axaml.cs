using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Financisto.Common.Model;

namespace Financisto.Common.Filters
{
    [ExcludeFromCodeCoverage]
    public partial class TagFilter : UserControl
    {
        public static readonly StyledProperty<ObservableCollection<TagModel>> SelectedTagsProperty =
            AvaloniaProperty.Register<TagFilter, ObservableCollection<TagModel>>(
                nameof(SelectedTags),
                defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public TagFilter() => InitializeComponent();

        public ObservableCollection<TagModel> SelectedTags
        {
            get => GetValue(SelectedTagsProperty);
            set => SetValue(SelectedTagsProperty, value);
        }
    }
}
