using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.Views.Dialogs;

namespace Financisto.Desktop.Helpers;

public class DialogWrapper : IDialogWrapper
{
    public async Task<object?> ShowDialogAsync<T>(DialogBaseVM context, double height, double width, string? title = null)
        where T : UserControl, new()
    {
        var owner = GetOwner();
        if (owner == null)
            return null;

        var dialog = new Window
        {
            Content = new T { DataContext = context },
            CanResize = false,
            Height = height,
            Width = width,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Title = title ?? "Financisto",
            ShowInTaskbar = Debugger.IsAttached,
        };

        context.RequestCancel += (_, _) => dialog.Close(null);
        context.RequestSave += (sender, _) => dialog.Close(sender);

        return await dialog.ShowDialog<object>(owner);
    }

    public async Task<string> OpenFileDialogAsync(string fileExtension)
    {
        var owner = GetOwner();
        if (owner == null)
            return string.Empty;

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType($"{fileExtension} files") { Patterns = new[] { $"*.{fileExtension}" } }
            }
        });

        return files.Count > 0 ? files[0].Path.LocalPath : string.Empty;
    }

    public async Task<string> SaveFileDialogAsync(string fileExtension, string defaultPath = "")
    {
        var owner = GetOwner();
        if (owner == null)
            return string.Empty;

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = defaultPath,
            DefaultExtension = fileExtension,
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType($"{fileExtension} files") { Patterns = new[] { $"*.{fileExtension}" } }
            }
        });

        return file != null ? file.Path.LocalPath : string.Empty;
    }

    public async Task<string> OpenFolderDialogAsync(string defaultPath = "")
    {
        var owner = GetOwner();
        if (owner == null)
            return string.Empty;

        IStorageFolder suggestedStartLocation = null;
        if (!string.IsNullOrEmpty(defaultPath))
        {
            suggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(defaultPath);
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStartLocation
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : string.Empty;
    }

    public async Task<bool> ShowMessageBoxAsync(string text, string caption, bool yesNoButtons = false)
    {
        var owner = GetOwner();
        if (owner == null)
            return true;

        var dialog = MessageBoxWindow.Create(text, caption, yesNoButtons);
        return await dialog.ShowDialog<bool>(owner);
    }

    private static Window? GetOwner()
    {
        return Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
    }
}
