using CommunityToolkit.Mvvm.ComponentModel;

namespace Financisto.Desktop.ViewModels.Wizards;

public abstract partial class WizardPageBaseVM : ObservableObject
{
    [ObservableProperty]
    private bool _isCurrentPage;

    public abstract string Title { get; }

    public abstract bool IsValid();
}
