
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using MiniExcelLibs;

namespace Financisto.BankHelpers.ABank
{
    public class AbankExcelHelper : BankHelperBase
    {
        public override string BankTitle => ABankInfo.Title;

        public override ReportType ReportType => ReportType.Xlsx;

        public override IEnumerable<BankTransaction> ParseReport(string filePath)
        {
            List<AbankRow> abankRows = new List<AbankRow>();
            var rows = MiniExcel.Query(filePath, useHeaderRow: true, excelType: ExcelType.XLSX, startCell: "A20");

            using (var csvStream = new MemoryStream())
            {
                MiniExcel.SaveAs(csvStream, rows, printHeader: true, excelType: ExcelType.CSV);

                using (var csvReader = new StreamReader(csvStream))
                using (var csv = new CsvReader(csvReader, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    IgnoreBlankLines = true,
                    ShouldSkipRecord = args => args.Row.Parser.Record!.All(string.IsNullOrWhiteSpace),
                    Delimiter = ","
                }))
                {
                    csvStream.Position = 0;
                    var r = csv.GetRecords<AbankRow>().ToList();
                    abankRows.AddRange(r);
                }
            }

            var transactions = abankRows.Select(ABankInfo.ToBankTransaction).ToList();
            return transactions;
        }
    }
}
