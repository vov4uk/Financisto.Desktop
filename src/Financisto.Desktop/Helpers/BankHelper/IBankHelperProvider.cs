using System.Collections.Generic;
using Financisto.BankHelpers;

namespace Financisto.Desktop.Helpers.BankHelper
{
    /// <summary>The bank statement readers available for the Import menu.</summary>
    public interface IBankHelperProvider
    {
        IReadOnlyList<IBankHelper> BankHelpers { get; }
    }
}
