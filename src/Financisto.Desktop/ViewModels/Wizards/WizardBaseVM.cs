using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace Financisto.Desktop.ViewModels.Wizards;

public abstract partial class WizardBaseVM : ObservableObject
{
    protected WizardPageBaseVM _currentPage;
    protected ReadOnlyCollection<WizardPageBaseVM> _pages;

    public event EventHandler<bool> RequestClose;

    public WizardPageBaseVM CurrentPage
    {
        get => _currentPage;
        protected set
        {
            if (value == _currentPage)
                return;

            BeforeCurrentPageUpdated(_currentPage, value);

            _currentPage = value;

            AfterCurrentPageUpdated(_currentPage);

            MovePreviousCommand.NotifyCanExecuteChanged();
            MoveNextCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(CurrentPage));
            OnPropertyChanged(nameof(IsOnLastPage));
        }
    }

    public bool IsOnLastPage => CurrentPageIndex == Pages.Count - 1;

    public ReadOnlyCollection<WizardPageBaseVM> Pages => _pages;

    public string Title => CurrentPage == null ? string.Empty : CurrentPage.Title;

    private int CurrentPageIndex => Pages.IndexOf(CurrentPage);

    public abstract void AfterCurrentPageUpdated(WizardPageBaseVM newValue);

    public abstract void BeforeCurrentPageUpdated(WizardPageBaseVM old, WizardPageBaseVM newValue);

    public abstract void CreatePages();

    public abstract object OnRequestClose(bool save);

    [RelayCommand]
    private void Cancel() => OnClose(false);

    [RelayCommand(CanExecute = nameof(CanMoveToNextPage))]
    private void MoveNext()
    {
        if (CurrentPageIndex < Pages.Count - 1)
            CurrentPage = Pages[CurrentPageIndex + 1];
        else
            OnClose(true);
    }

    private bool CanMoveToNextPage() => CurrentPage != null && CurrentPage.IsValid();

    [RelayCommand(CanExecute = nameof(CanMoveToPreviousPage))]
    private void MovePrevious() => CurrentPage = Pages[CurrentPageIndex - 1];

    private bool CanMoveToPreviousPage() => 0 < CurrentPageIndex;

    private void OnClose(bool save)
    {
        var output = OnRequestClose(save);
        RequestClose?.Invoke(output, save);
    }
}
