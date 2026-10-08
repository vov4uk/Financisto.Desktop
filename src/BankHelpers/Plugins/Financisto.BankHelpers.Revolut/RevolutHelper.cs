using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Financisto.BankHelpers.Revolut
{
    public class RevolutHelper : BankHelperBase
    {
        public override string BankTitle => "Revolut";

        public override ReportType ReportType => ReportType.Csv;

        public override IEnumerable<BankTransaction> ParseReport(string filePath)
        {
            if (!File.Exists(filePath))
                return Array.Empty<BankTransaction>();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
                HasHeaderRecord = true,
            };

            using FileStream file = File.OpenRead(filePath);
            using StreamReader reader = new StreamReader(file, Encoding.UTF8);
            using var csv = new CsvReader(reader, config);

            return csv.GetRecords<RevolutRow>()
                .Select(r => new BankTransaction
                {
                    Date = r.StartDate,
                    Description = r.Description,
                    CardCurrencyAmount = r.Amount,
                    OperationAmount = r.Amount,
                    OperationCurrency = r.Currency,
                    Commission = r.Fee,
                    Balance = BankParsing.GetDouble(r.Balance),
                })
                .OrderBy(t => t.Date)
                .ToList();
        }
    }
}
