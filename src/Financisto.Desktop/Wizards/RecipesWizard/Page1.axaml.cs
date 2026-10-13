using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Financisto.Common.Localization;
using Financisto.Desktop.Helpers;
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

    // The text of the chosen PDF, HTML or text file goes into the text field (Page1VM.LoadFileAsync reports a file without text).
    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage || DataContext is not Page1VM page)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = LocalizationService.Instance["open_file"],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(LocalizationService.Instance["receipt_files"]) { Patterns = ReceiptFiles.Patterns },
                FilePickerFileTypes.Pdf,
                new FilePickerFileType("HTML")
                {
                    Patterns = ReceiptFiles.HtmlExtensions.Select(extension => "*" + extension).ToArray(),
                    MimeTypes = new[] { "text/html" },
                    AppleUniformTypeIdentifiers = new[] { "public.html" },
                },
                FilePickerFileTypes.TextPlain,
            },
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            await page.LoadFileAsync(path);
        }
    }
}
