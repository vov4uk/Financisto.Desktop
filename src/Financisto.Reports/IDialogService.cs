namespace Financisto.Reports
{
    /// <summary>Lets a report tell the user something (e.g. a missing filter) without knowing about windows.</summary>
    public interface IDialogService
    {
        void ShowMessage(string message);
    }
}
