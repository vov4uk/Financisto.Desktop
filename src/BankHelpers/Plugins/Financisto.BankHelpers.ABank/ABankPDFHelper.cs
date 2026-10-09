using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper;
using Financisto.BankHelpers.Pdf;

namespace Financisto.BankHelpers.ABank
{
    public class ABankPDFHelper : BankPdfHelperBase
    {
        public override string BankTitle => ABankInfo.Title;

        protected override IEnumerable<BankTransaction> ParseTransactionsTable(IEnumerable<string> pages)
        {
            List<AbankRow> abankRows = new List<AbankRow>();

            foreach (var page in pages)
            {
                using (var csv = new CsvReader(new StringReader(page), DefaultCsvReaderConfig))
                {
                    var records = csv.GetRecords<AbankRow>().ToList();
                    abankRows.AddRange(records);
                }
            }

            var transactions = abankRows.Select(ABankInfo.ToBankTransaction).ToList();
            return transactions;
        }
    }
}
