namespace Financisto.Desktop.Tests.Wizards.Mono.Erste
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Financisto.BankHelpers;
    using Financisto.BankHelpers.Erste;
    using Financisto.Desktop.Helpers.BankHelper;
    using Financisto.Desktop.Wizards;
    using Xunit;

    public class ErsteHelperTest
    {
        private const string StatementLine = "2026-10-07,16-09-2026,'11 1111 1111 0000 0000 0000 0001,JAN KOWALSKI,PLN,\"0,00\",\"100,00\",1,";

        [Fact]
        public void ParseReport_FileDoesNotExist_ReturnsEmpty()
        {
            var path = Path.Combine(Environment.CurrentDirectory, "Assets", Guid.NewGuid().ToString());
            IEnumerable<BankTransaction> result = new ErsteHelper().ParseReport(path);

            Assert.Empty(result);
        }

        [Fact]
        public void ParseReport_ValidCsv_SkipsStatementLine()
        {
            var result = ParseSample();

            Assert.Equal(12, result.Count);
            Assert.DoesNotContain(result, r => r.Description.Contains("11 1111 1111"));
        }

        [Fact]
        public void ParseReport_ValidCsv_NewestFirstAndDatesAreUnique()
        {
            var result = ParseSample();

            Assert.Equal(result.OrderByDescending(r => r.Date).ToList(), result);
            Assert.Equal(result.Count, result.Select(r => r.Date).Distinct().Count());
        }

        [Fact]
        public void ParseReport_ValidCsv_CardRowMappedCorrectly()
        {
            var first = ParseSample()[0];

            Assert.Equal(new DateTime(2026, 10, 5), first.Date);
            Assert.Equal("VISA PLAT 421352******8814 PRZELEW KARTĄ 50.00 PLN\r\nRevolut**3169* Dublin", first.Description);
            Assert.Equal(-50.00, first.CardCurrencyAmount);
            Assert.Equal(-50.00, first.OperationAmount);
            Assert.Equal("PLN", first.OperationCurrency);
            Assert.Null(first.Commission);
            Assert.Equal(6296.08, first.Balance);
        }

        [Fact]
        public void ParseReport_CardPayment_DropsPaymentWordsAndBreaksLineAfterAmount()
        {
            var hotel = ParseSample().Single(r => r.Description.Contains("HOTELLIBERTE33"));

            Assert.Equal("VISA PLAT 421352******8814 220.15 PLN\r\nMS* HOTELLIBERTE33 KRAKOW", hotel.Description);
            Assert.Equal(-220.15, hotel.CardCurrencyAmount);
        }

        [Fact]
        public void ParseReport_CardPayments_NoneKeepThePaymentWords()
        {
            var cardRows = ParseSample().Where(r => r.Description.StartsWith("VISA")).ToList();

            Assert.Equal(9, cardRows.Count);
            Assert.All(cardRows, r => Assert.DoesNotContain("PŁATNOŚĆ", r.Description));
            Assert.All(cardRows, r => Assert.Contains("PLN\r\n", r.Description));
        }

        [Fact]
        public void ParseReport_CardPaymentWithoutMerchant_HasNoTrailingNewLine()
        {
            var result = ParseLines(
                StatementLine,
                "07-10-2026,06-10-2026,VISA PLAT 421352******8814 PŁATNOŚĆ KARTĄ 44.37 PLN,,,\"-44,37\",\"100,00\",1,");

            Assert.Equal("VISA PLAT 421352******8814 44.37 PLN", Assert.Single(result).Description);
        }

        [Fact]
        public void ParseReport_ValidCsv_UsesSecondColumnDateNotBookingDate()
        {
            var hotel = ParseSample().Single(r => r.Description.Contains("HOTELLIBERTE33"));

            // Booked 05-10-2026 (first column), paid 03-10-2026 (second column).
            Assert.Equal(new DateTime(2026, 10, 3), hotel.Date.Date);
        }

        [Fact]
        public void ParseReport_SecondColumnDateMissing_FallsBackToBookingDate()
        {
            var result = ParseLines(
                StatementLine,
                "07-10-2026,,VISA PLAT 421352******8814 PŁATNOŚĆ KARTĄ 9.00 PLN Coffee,,,\"-9,00\",\"91,00\",1,");

            Assert.Equal(new DateTime(2026, 10, 7), Assert.Single(result).Date);
        }

        [Fact]
        public void ParseReport_SameDayRows_KeepFileOrderWithMinutes()
        {
            var paid03 = ParseSample().Where(r => r.Date.Date == new DateTime(2026, 10, 3)).ToList();

            // The file lists these newest first (balances 6649.08, 6355.08, 6626.08, 6400.08), so the last one is the oldest.
            Assert.Equal(new[] { 6649.08, 6355.08, 6626.08, 6400.08 }, paid03.Select(r => r.Balance));
            Assert.Equal(
                new[]
                {
                    new DateTime(2026, 10, 3, 0, 3, 0),
                    new DateTime(2026, 10, 3, 0, 2, 0),
                    new DateTime(2026, 10, 3, 0, 1, 0),
                    new DateTime(2026, 10, 3, 0, 0, 0),
                },
                paid03.Select(r => r.Date));
        }

        [Fact]
        public void ParseReport_MoreThanSixtyRowsInADay_RollsOverToHours()
        {
            var lines = new[] { StatementLine }
                .Concat(Enumerable.Range(0, 61).Select(i => $"07-10-2026,06-10-2026,Row {i},,,\"-1,00\",\"{100 - i},00\",{i},"))
                .ToArray();

            var result = ParseLines(lines);

            Assert.Equal(61, result.Count);
            Assert.Equal("Row 0", result[0].Description);
            Assert.Equal(new DateTime(2026, 10, 6, 1, 0, 0), result[0].Date);
            Assert.Equal("Row 60", result[^1].Description);
            Assert.Equal(new DateTime(2026, 10, 6, 0, 0, 0), result[^1].Date);
        }

        [Fact]
        public void ParseReport_TransferTitle_GetsCounterparty()
        {
            var income = ParseSample().Last();

            Assert.Equal(new DateTime(2026, 9, 16), income.Date);
            Assert.Equal("SIERPIEŃ : JAN KOWALSKI ELIXIR 15-09-2026", income.Description);
            Assert.Equal(10000.00, income.CardCurrencyAmount);
            Assert.Equal(10000.00, income.Balance);
        }

        [Fact]
        public void ParseReport_BlikTitleAlreadyNamesCounterparty_NotRepeated()
        {
            var blik = ParseSample().Single(r => r.Description.Contains("tpay.com"));

            Assert.Equal("Zakup BLIK tpay.com plac Andersa 3 7361-894 Poznan ref:95057773731", blik.Description);
            Assert.Equal(-108.00, blik.CardCurrencyAmount);
        }

        [Fact]
        public void ParseReport_ForeignCurrencyCardPayment_KeepsOriginalAmount()
        {
            var result = ParseLines(
                StatementLine,
                "07-10-2026,05-10-2026,VISA PLAT 421352******8814 PŁATNOŚĆ KARTĄ 25.00 EUR SHOP Berlin,,,\"-107,50\",\"100,00\",1,");

            var payment = Assert.Single(result);
            Assert.Equal("VISA PLAT 421352******8814 25.00 EUR\r\nSHOP Berlin", payment.Description);
            Assert.Equal(-107.50, payment.CardCurrencyAmount);
            Assert.Equal(-25.00, payment.OperationAmount);
            Assert.Equal("EUR", payment.OperationCurrency);
        }

        [Fact]
        public void ParseReport_SameDayRowsWithEqualAmounts_KeepDistinctDates()
        {
            var result = ParseLines(
                StatementLine,
                "07-10-2026,06-10-2026,VISA PLAT 421352******8814 PŁATNOŚĆ KARTĄ 9.00 PLN Coffee,,,\"-9,00\",\"82,00\",2,",
                "07-10-2026,06-10-2026,VISA PLAT 421352******8814 PŁATNOŚĆ KARTĄ 9.00 PLN Coffee,,,\"-9,00\",\"91,00\",1,");

            Assert.Equal(2, result.Count);
            Assert.NotEqual(result[0].Date, result[1].Date);
            Assert.Equal(82.00, result[0].Balance);
        }

        [Fact]
        public void ErsteHelper_DescribesItself()
        {
            IBankHelper helper = new ErsteHelper();

            Assert.Equal("Erste", helper.BankTitle);
            Assert.Equal(ReportType.Csv, helper.ReportType);
            Assert.NotEmpty(helper.Icon!);
        }

        private static List<BankTransaction> ParseSample()
        {
            var path = Path.Combine(Environment.CurrentDirectory, "Assets", "erste.csv");
            return new ErsteHelper().ParseReport(path).ToList();
        }

        private static List<BankTransaction> ParseLines(params string[] lines)
        {
            var path = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(path, lines);
                return new ErsteHelper().ParseReport(path).ToList();
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
