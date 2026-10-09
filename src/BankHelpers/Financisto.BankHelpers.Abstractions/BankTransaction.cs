using System;
using System.Diagnostics;

namespace Financisto.BankHelpers
{
    /// <summary>One bank statement row in Monobank's layout; every bank helper maps its rows into it. Amounts are in currency units.</summary>
    [DebuggerDisplay("{Description} : {CardCurrencyAmount} : {Balance}")]
    public class BankTransaction
    {
        public DateTime Date { get; set; }

        public string Description { get; set; } = string.Empty;

        public string? MCC { get; set; }

        public double CardCurrencyAmount { get; set; }

        public double OperationAmount { get; set; }

        /// <summary>Set only when the operation currency differs from the card currency.</summary>
        public string? OperationCurrency { get; set; }

        public double? ExchangeRate { get; set; }

        public double? Commission { get; set; }

        public double? Cashback { get; set; }

        public double Balance { get; set; }
    }
}
