using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Financisto.Common.Model;

namespace Financisto.Common.Filters
{
    [ExcludeFromCodeCoverage]
    public partial class TagFilter : UserControl
    {
        public static readonly StyledProperty<IList<TagModel>> SelectedTagsProperty =
            AvaloniaProperty.Register<TagFilter, IList<TagModel>>(
                nameof(SelectedTags),
                defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public TagFilter() => InitializeComponent();

        public IList<TagModel> SelectedTags
        {
            get => GetValue(SelectedTagsProperty);
            set => SetValue(SelectedTagsProperty, value);
        }
    }
}
