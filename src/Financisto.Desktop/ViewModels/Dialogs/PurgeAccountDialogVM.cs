using System;
using Financisto.Common.Localization;

namespace Financisto.Desktop.ViewModels.Dialogs;

/// <summary>
/// Android's <c>PurgeAccountActivity</c>: asks up to which date (inclusive) the account's transactions are deleted. Returns that date as a
/// <see cref="DateTime"/>. Android's "database backup first" option isn't offered: the loaded backup file only changes on Save backup.
/// </summary>
public class PurgeAccountDialogVM : DialogBaseVM
{
    private DateTime date;

    public PurgeAccountDialogVM(string accountTitle, DateTime date)
    {
        AccountTitle = accountTitle;
        this.date = date;
    }

    public string AccountTitle { get; }

    public string Warning => LocalizationService.Instance.purge_account_date_summary;

    public DateTime Date
    {
        get => date;
        set => SetProperty(ref date, value);
    }

    public override object OnRequestSave() => Date;
}
