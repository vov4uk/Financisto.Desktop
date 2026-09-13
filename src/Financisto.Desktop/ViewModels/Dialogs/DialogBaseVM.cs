using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace Financisto.Desktop.ViewModels.Dialogs;

public abstract partial class DialogBaseVM : ObservableObject
{
    public event EventHandler RequestCancel;

    public event EventHandler RequestSave;

    public abstract object OnRequestSave();

    protected virtual bool CanSaveCommandExecute() => true;

    [RelayCommand]
    private void Cancel() => RequestCancel?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanSaveCommandExecute))]
    private void Save()
    {
        var output = OnRequestSave();
        RequestSave?.Invoke(output, EventArgs.Empty);
    }
}
