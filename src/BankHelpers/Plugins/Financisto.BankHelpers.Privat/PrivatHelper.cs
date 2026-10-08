using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using MiniExcelLibs;
using static Financisto.BankHelpers.BankParsing;

namespace Financisto.BankHelpers.Privat
{
    public class PrivatHelper : BankHelperBase
    {
        public override string BankTitle => Localized("Privat", ("uk", "Приват"));

        public override ReportType ReportType => ReportType.Xlsx;

        public override IEnumerable<BankTransaction> ParseReport(string filePath)
        {
            List<PrivatRow> abankRows = new List<PrivatRow>();
            var rows = MiniExcel.Query(filePath, useHeaderRow: true, excelType: ExcelType.XLSX, startCell: "A2");

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
                    var r = csv.GetRecords<PrivatRow>().ToList();
                    abankRows.AddRange(r);
                }
            }

            var transactions = abankRows.Select(ToBankTransaction).ToList();
            return transactions;
        }

        private static BankTransaction ToBankTransaction(PrivatRow item)
        {
            var operationCurrency = item.OperationCurrency;
            var operationAmount = GetDouble(item.OperationAmount);
            var cardCurrencyAmount = GetDouble(item.CardCurrencyAmount);
            if (cardCurrencyAmount < 0)
            {
                operationAmount = -1 * Math.Abs(operationAmount);
            }

            return new BankTransaction
            {
                Balance = GetDouble(item.Balance),
                OperationCurrency = Math.Abs(operationAmount) != Math.Abs(cardCurrencyAmount) ? operationCurrency : null,
                OperationAmount = operationAmount,
                CardCurrencyAmount = cardCurrencyAmount,
                Description = $"{item.Category} : {item.Details}",
                Date = ParseDateTime(item.Date)
            };
        }
    }
}
