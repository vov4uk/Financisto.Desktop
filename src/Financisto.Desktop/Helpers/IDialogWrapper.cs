using System.Threading.Tasks;
using Avalonia.Controls;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.Wizards;

namespace Financisto.Desktop.Helpers;

public interface IDialogWrapper
{
    Task<object?> ShowDialogAsync<T>(DialogBaseVM context, double height, double width, string title = null)
        where T : UserControl, new();

    /// <summary>Shows the wizard modally; returns its output when finished, null when cancelled or closed.</summary>
    Task<object?> ShowWizardAsync(WizardBaseVM context);

    Task<string> OpenFileDialogAsync(string fileExtention);

    Task<string> SaveFileDialogAsync(string fileExtention, string defaultPath = "");

    Task<string> OpenFolderDialogAsync(string defaultPath = "");

    Task<bool> ShowMessageBoxAsync(string text, string caption, bool yesNoButtons = false);
}
