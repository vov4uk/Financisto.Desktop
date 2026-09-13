using Avalonia.Controls;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.ViewModels.Wizards;
using System.Threading.Tasks;

namespace Financisto.Desktop.Helpers;

public interface IDialogWrapper
{
    Task<object?> ShowDialogAsync<T>(DialogBaseVM context, double height, double width, string? title = null)
        where T : UserControl, new();

    Task<string> OpenFileDialogAsync(string fileExtension);

    Task<string> SaveFileDialogAsync(string fileExtension, string defaultPath = "");

    Task<object?> ShowWizardAsync(WizardBaseVM context);

    Task<bool> ShowMessageBoxAsync(string text, string caption, bool yesNoButtons = false);
}
