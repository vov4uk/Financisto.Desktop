using System;
using System.Collections.ObjectModel;
using Financisto.Common.Localization;
using Prism.Commands;
using Prism.Mvvm;

namespace Financisto.Desktop.Wizards
{
    /// <summary>
    /// A multi-page wizard shown by <see cref="Helpers.IDialogWrapper.ShowWizardAsync"/> in a <see cref="WizardWindow"/>.
    /// <see cref="RequestClose"/> passes the wizard output as the sender and whether it was finished (true) or cancelled.
    /// </summary>
    public abstract class WizardBaseVM : BindableBase
    {
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        protected WizardPageBaseVM _currentPage;
        protected ReadOnlyCollection<WizardPageBaseVM> _pages;
        private DelegateCommand _cancelCommand;
        private DelegateCommand _moveNextCommand;
        private DelegateCommand _movePreviousCommand;
        private string? errorMessage;
        public event EventHandler<bool> RequestClose;

        /// <summary>Why the wizard could not finish (shown above its buttons); empty when there is no problem. Cleared by the next try and by a page change.</summary>
        public string? ErrorMessage
        {
            get => errorMessage;
            private set => SetProperty(ref errorMessage, value);
        }

        public DelegateCommand CancelCommand
        {
            get
            {
                return _cancelCommand ??= new DelegateCommand(() => OnClose(false));
            }
        }

        public WizardPageBaseVM CurrentPage
        {
            get => _currentPage;
            protected set
            {
                if (value == _currentPage)
                    return;

                ErrorMessage = null;

                BeforeCurrentPageUpdated(_currentPage, value);

                if (_currentPage != null)
                    _currentPage.IsCurrentPage = false;

                _currentPage = value;

                // WizardWindow keeps every page's view alive and shows the current one.
                if (_currentPage != null)
                    _currentPage.IsCurrentPage = true;

                AfterCurrentPageUpdated(_currentPage);

                MovePreviousCommand.RaiseCanExecuteChanged();
                MoveNextCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(Title));
                RaisePropertyChanged(nameof(CurrentPage));
                RaisePropertyChanged(nameof(IsOnLastPage));
            }
        }

        public bool IsOnLastPage => CurrentPageIndex == Pages.Count - 1;

        public DelegateCommand MoveNextCommand
        {
            get
            {
                return _moveNextCommand ??= new DelegateCommand(MoveToNextPage, () => CanMoveToNextPage);
            }
        }

        public DelegateCommand MovePreviousCommand
        {
            get
            {
                return _movePreviousCommand ??= new DelegateCommand(MoveToPreviousPage, () => CanMoveToPreviousPage);
            }
        }

        public ReadOnlyCollection<WizardPageBaseVM> Pages => _pages;

        public string Title => CurrentPage == null ? string.Empty : CurrentPage.Title;

        bool CanMoveToNextPage => CurrentPage != null && CurrentPage.IsValid();

        bool CanMoveToPreviousPage => 0 < CurrentPageIndex;

        int CurrentPageIndex
        {
            get
            {
                if (CurrentPage == null)
                {
                    Logger.Error("Why is the current page null?");
                }
                return Pages.IndexOf(CurrentPage);
            }
        }

        public abstract void AfterCurrentPageUpdated(WizardPageBaseVM newValue);

        public abstract void BeforeCurrentPageUpdated(WizardPageBaseVM old, WizardPageBaseVM newValue);
        public abstract void CreatePages();
        public abstract object OnRequestClose(bool save);

        void MoveToNextPage()
        {
            if (CanMoveToNextPage)
            {
                if (CurrentPageIndex < Pages.Count - 1)
                    CurrentPage = Pages[CurrentPageIndex + 1];
                else
                    OnClose(true);
            }
        }
        void MoveToPreviousPage()
        {
            if (CanMoveToPreviousPage)
                CurrentPage = Pages[CurrentPageIndex - 1];
        }

        void OnClose(bool save)
        {
            Logger.Info($"[{GetType().Name}] closing, save={save}");
            ErrorMessage = null;
            object output;
            try
            {
                output = OnRequestClose(save);
            }
            catch (Exception ex)
            {
                // The wizard stays open: nothing is lost and the user can try again or cancel.
                Logger.Error(ex, $"[{GetType().Name}] its result could not be made (save={save}), the wizard stays open");
                ErrorMessage = string.Format(LocalizationService.Instance["wizard_failed"], ex.Message);
                return;
            }

            RequestClose?.Invoke(output, save);
            Logger.Info($"[{GetType().Name}] closed");
        }
    }
}
