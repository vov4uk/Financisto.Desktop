using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Financisto.Common.Localization;
using Financisto.Desktop.Wizards.RecipesWizard.ViewModel;

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

    // The text layer of the chosen PDF goes into the text field (Page1VM.LoadPdfAsync reports a PDF without text).
    private async void OnOpenPdfClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage || DataContext is not Page1VM page)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Instance["open_pdf"],
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.Pdf },
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await page.LoadPdfAsync(path);
        }
    }
}
