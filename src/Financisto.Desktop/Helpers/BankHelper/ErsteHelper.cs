using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Financisto.Common.Localization;
using Financisto.Common.Utils;
using Financisto.Desktop.Wizards;

namespace Financisto.Desktop.Helpers.BankHelper
{
    /// <summary>
    /// Erste Bank Polska "historia" CSV: no header row, the first line describes the statement
    /// (<c>report date, period start, 'account, owner, currency, opening balance, closing balance, rows</c>),
    /// every other line is <c>booking date, transaction date, title, counterparty, counterparty account, amount, balance, no.</c>
    /// with dd-MM-yyyy dates and decimal commas. Rows are listed newest first.
    /// </summary>
    public class ErsteHelper : IBankHelper
    {
        private const string DateFormat = "dd-MM-yyyy";

        private const string NewLine = "\r\n";

        // Card titles state the original amount: "... PŁATNOŚĆ KARTĄ 9.00 PLN Merchant City".
        private static readonly Regex CardAmountRegex = new(
            @"(?<payment>PŁATNOŚĆ\s+)?KART[ĄA]\s+(?<amount>\d+(?:[.,]\d+)?)\s+(?<currency>[A-Z]{3})\b\s*",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));

        public string BankTitle => LocalizationService.Instance.erste;

        public IEnumerable<BankTransaction> ParseReport(string filePath)
        {
            if (!File.Exists(filePath))
                return Array.Empty<BankTransaction>();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false,
                IgnoreBlankLines = true,
                MissingFieldFound = null,
            };

            using FileStream file = File.OpenRead(filePath);
            using StreamReader reader = new StreamReader(file, Encoding.UTF8);
            using var csv = new CsvReader(reader, config);

            var accountCurrency = string.Empty;
            var rows = new List<ErsteRow>();
            while (csv.Read())
            {
                if (!DateTime.TryParseExact(csv.GetField(0), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var bookingDate))
                {
                    // The statement line starts with an ISO date; it is the only place that names the account currency.
                    if (accountCurrency.Length == 0)
                        accountCurrency = csv.GetField(4)?.Trim() ?? string.Empty;
                    continue;
                }

                // The second column is when the operation happened; the booking date (first column) only backs it up.
                var date = DateTime.TryParseExact(csv.GetField(1), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var transactionDate)
                    ? transactionDate
                    : bookingDate;

                rows.Add(new ErsteRow
                {
                    Date = date,
                    Title = csv.GetField(2)?.Trim() ?? string.Empty,
                    Counterparty = csv.GetField(3)?.Trim() ?? string.Empty,
                    Amount = DoubleUtils.GetDouble(csv.GetField(5)),
                    Balance = DoubleUtils.GetDouble(csv.GetField(6)),
                });
            }

            return ToTransactions(rows, accountCurrency).OrderByDescending(t => t.Date).ToList();
        }

        private static IEnumerable<BankTransaction> ToTransactions(List<ErsteRow> rows, string accountCurrency)
        {
            foreach (var day in rows.GroupBy(r => r.Date).OrderBy(g => g.Key))
            {
                // The statement has no times but lists rows newest first, so the oldest row of a day gets 00:00
                // and every newer one a minute more (the hour rolls over after 59 rows). That keeps the statement's order,
                // stops the importer's (account, time, amount) duplicate check from collapsing same-day rows, and gives
                // the wizard's "newer than the selected transaction" filter something to compare within a day.
                var minutes = 0;
                foreach (var row in day.Reverse())
                {
                    var (operationAmount, operationCurrency) = GetOperationAmount(row, accountCurrency);
                    yield return new BankTransaction
                    {
                        Date = row.Date.AddMinutes(minutes++),
                        Description = GetDescription(row),
                        CardCurrencyAmount = row.Amount,
                        OperationAmount = operationAmount,
                        OperationCurrency = operationCurrency,
                        Balance = row.Balance,
                    };
                }
            }
        }

        private static string GetDescription(ErsteRow row)
        {
            // "<card> PŁATNOŚĆ KARTĄ 44.37 PLN <merchant>" becomes "<card> 44.37 PLN" + newline + "<merchant>"; any other
            // card operation (e.g. PRZELEW KARTĄ) keeps its wording and only gets the newline after the amount.
            var title = CardAmountRegex.Replace(
                row.Title,
                m => (m.Groups["payment"].Success ? $"{m.Groups["amount"].Value} {m.Groups["currency"].Value}" : m.Value.TrimEnd()) + NewLine,
                1).Trim();

            // A title is often just a bare reference ("SIERPIEŃ"); the counterparty says who it was with, unless the title already does.
            return row.Counterparty.Length == 0 || row.Title.Contains(row.Counterparty, StringComparison.OrdinalIgnoreCase)
                ? title
                : $"{title} : {row.Counterparty}";
        }

        private static (double Amount, string Currency) GetOperationAmount(ErsteRow row, string accountCurrency)
        {
            var match = CardAmountRegex.Match(row.Title);
            if (match.Success && accountCurrency.Length > 0 && !match.Groups["currency"].Value.Equals(accountCurrency, StringComparison.OrdinalIgnoreCase))
            {
                var original = DoubleUtils.GetDouble(match.Groups["amount"].Value);
                return (row.Amount < 0 ? -original : original, match.Groups["currency"].Value.ToUpperInvariant());
            }

            return (row.Amount, accountCurrency);
        }

        private sealed class ErsteRow
        {
            public DateTime Date { get; init; }
            public string Title { get; init; }
            public string Counterparty { get; init; }
            public double Amount { get; init; }
            public double Balance { get; init; }
        }
    }
}
