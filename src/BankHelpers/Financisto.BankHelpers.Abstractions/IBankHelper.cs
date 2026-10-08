using System.Collections.Generic;

namespace Financisto.BankHelpers
{
    /// <summary>
    /// Reads one kind of bank statement. A plugin is a single DLL in the app's <c>plugins</c> folder
    /// that holds one or more public, non-abstract classes implementing this interface and having a public
    /// parameterless constructor; the app lists each of them in the Import menu.
    /// Instances are created once and reused for every import, so keep them stateless.
    /// </summary>
    public interface IBankHelper
    {
        /// <summary>The name shown in the Import menu and used to pick the matching account. May depend on <c>CultureInfo.CurrentUICulture</c>.</summary>
        string BankTitle { get; }

        /// <summary>The format of the statement file; it groups the menu and filters the file dialog.</summary>
        ReportType ReportType { get; }

        /// <summary>The bank's logo as encoded image bytes (PNG, JPEG, ...), or null for no icon.</summary>
        byte[]? Icon { get; }

        /// <summary>Reads the statement. Called on a worker thread; throw on an unreadable file.</summary>
        IEnumerable<BankTransaction> ParseReport(string filePath);
    }
}
