using System.Collections.Generic;
using Financisto.Desktop.Wizards;

namespace Financisto.Desktop.Helpers.BankHelper
{
    public interface IBankHelper
    {
        string BankTitle { get; }
        IEnumerable<BankTransaction> ParseReport(string filePath);
    }
}
