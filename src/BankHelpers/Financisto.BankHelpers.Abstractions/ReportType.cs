namespace Financisto.BankHelpers
{
    /// <summary>The file format of the statement a helper reads.</summary>
    public enum ReportType
    {
        Csv,
        Xlsx,
        Xls,
        Pdf,
        Json,
        Xml,
        Txt,
    }

    public static class ReportTypeExtensions
    {
        /// <summary>The file extension without the dot (<c>csv</c>, <c>pdf</c>, ...), used to filter the file dialog.</summary>
        public static string GetFileExtension(this ReportType reportType) => reportType.ToString().ToLowerInvariant();
    }
}
