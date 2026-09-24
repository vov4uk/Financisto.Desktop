using System;
using Avalonia.Controls;
using Financisto.Desktop.ViewModels.Pages;

namespace Financisto.Desktop.Views;

public partial class TagPageView : UserControl
{
    public TagPageView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        // DataGrid columns aren't in the logical tree, so IsVisible can't bind to the page view model.
        foreach (var column in EntitiesGrid.Columns)
        {
            if (Equals(column.Tag, "aliases"))
                column.IsVisible = DataContext is ITagBaseVM { HasAliases: true };
        }
    }
}
