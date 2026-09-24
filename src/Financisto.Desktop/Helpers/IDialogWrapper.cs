using System.Threading.Tasks;
using Avalonia.Controls;
using Financisto.Desktop.ViewModels.Dialogs;

namespace Financisto.Desktop.Helpers;

public interface IDialogWrapper
{
    Task<object?> ShowDialogAsync<T>(DialogBaseVM context, double height, double width, string title = null)
        where T : UserControl, new();

    Task<string> OpenFileDialogAsync(string fileExtention);

    Task<string> SaveFileDialogAsync(string fileExtention, string defaultPath = "");

    Task<string> OpenFolderDialogAsync(string defaultPath = "");

    Task<bool> ShowMessageBoxAsync(string text, string caption, bool yesNoButtons = false);
}
