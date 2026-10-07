using System;
using System.Threading.Tasks;
using Financisto.Common.Localization;
using Financisto.Reports;

namespace Financisto.Desktop.Helpers;

/// <summary>Shows the reports' messages (e.g. a missing filter) in the app's message box.</summary>
public sealed class ReportDialogService(IDialogWrapper dialogWrapper) : IDialogService
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    // IDialogService.ShowMessage can't be awaited; the report just carries on (with no data), so the box isn't waited for.
    public void ShowMessage(string message) => _ = ShowAsync(message);

    private async Task ShowAsync(string message)
    {
        try
        {
            await dialogWrapper.ShowMessageBoxAsync(message, LocalizationService.Instance.reports);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not show a report message: " + message);
        }
    }
}
