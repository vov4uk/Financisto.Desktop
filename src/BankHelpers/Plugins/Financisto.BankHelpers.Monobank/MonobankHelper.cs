using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace Financisto.BankHelpers.Monobank
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class MonobankHelper : BankHelperBase
    {
        public override string BankTitle => Localized("Monobank", ("uk", "Монобанк"));

        public override ReportType ReportType => ReportType.Csv;

        public override IEnumerable<BankTransaction> ParseReport(string filePath)
        {
            if (File.Exists(filePath))
            {
                var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    MissingFieldFound = null,
                    HasHeaderRecord = true,
                    ShouldSkipRecord = (e) =>
                    {
                        return e.Row.Parser.RawRecord.Contains("www.fg.gov.ua");
                    }
                };

                using FileStream file = File.OpenRead(filePath);
                using StreamReader streamReader = new StreamReader(file, Encoding.UTF8);
                using (var csv = new CsvReader(streamReader, config))
                {
                    csv.Context.RegisterClassMap<MonobankMap>();
                    return csv.GetRecords<BankTransaction>().ToList();
                }
            }
            return Array.Empty<BankTransaction>();
        }
    }
}
