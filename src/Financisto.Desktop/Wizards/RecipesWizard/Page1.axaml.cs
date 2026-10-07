using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Financisto.Desktop.Wizards.RecipesWizard.View;

[ExcludeFromCodeCoverage]
public partial class Page1 : UserControl
{
    public Page1()
    {
        InitializeComponent();
    }

    // Pastes at the caret like the WPF ApplicationCommands.Paste did.
    private void OnPasteClick(object? sender, RoutedEventArgs e)
    {
        RecipeText.Focus();
        RecipeText.Paste();
    }
}
