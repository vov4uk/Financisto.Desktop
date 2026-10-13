namespace Financisto.Desktop.Tests.Wizards.Recipes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Wizards.RecipesWizard.ViewModel;
    using Xunit;

    /// <summary>A receipt saved as an HTML page (a Lidl e-receipt): its text goes to the text field and is read like any other receipt.</summary>
    public sealed class ReceiptHtmlTests : IDisposable
    {
        private static readonly string Sample = Path.Combine(AppContext.BaseDirectory, "Assets", "lidl_receipt.html");

        private readonly string dir = Path.Combine(Path.GetTempPath(), "Financisto.Desktop.Tests", "html", Guid.NewGuid().ToString("N"));

        public ReceiptHtmlTests()
        {
            Directory.CreateDirectory(this.dir);
            DbManual.SetupTests(new List<CategoryModel>());
        }

        public void Dispose()
        {
            DbManual.ResetAllDatabaseManuals();
            Directory.Delete(this.dir, true);
        }

        // ---- HtmlTextExtractor

        [Fact]
        public void ExtractText_LidlReceipt_OneTextLinePerPrintedLine()
        {
            var lines = HtmlTextExtractor.ExtractTextFromFile(Sample).Split(Environment.NewLine);

            Assert.Equal("Szparagi zielon.500g", lines.Single(line => line.StartsWith("Szparagi")));
            Assert.Contains("                     2 * 19.99 39.98 C", lines);       // the alignment is kept
            Assert.Contains("   Taniej za 2                -20,00", lines);
            Assert.Contains("   Kaucja PET              1 * 0.5 0.5", lines);
            Assert.Matches(@"^Suma PLN\s+155,73$", lines.Single(line => line.StartsWith("Suma PLN")));   // a line made of several spans
            Assert.Matches(@"^Suma\s+156,23$", lines.Single(line => line.StartsWith("Suma ") && line.EndsWith("156,23")));
            Assert.DoesNotContain(lines, line => line.Contains('<') || line.Contains('>') || line.Contains("&#") || line.Contains("&oacute;"));
        }

        [Fact]
        public void ExtractText_Entities_Decoded()
        {
            var text = HtmlTextExtractor.ExtractTextFromFile(Sample);

            Assert.Contains("Podgórne nr rej: BDO", text);   // &oacute;
            Assert.Contains("Lay's Oven Baked", text);       // &#39;
            Assert.Contains("¦     Z Lidl Plus zaoszczędzono      ¦", text); // &brvbar;
        }

        [Fact]
        public void ExtractText_PageStyleAndTitle_NotInTheText()
        {
            var text = HtmlTextExtractor.ExtractTextFromFile(Sample);

            Assert.DoesNotContain("css_doublehigh", text);
            Assert.DoesNotContain("font-family", text);
            Assert.DoesNotContain("26.05.16_22.17_pln_156.23", text); // the <title>
        }

        [Fact]
        public void ExtractText_PageWithoutPre_ReadLikeABrowserDoes()
        {
            var html = "<html><head><title>x</title><style>.a{color:red}</style></head><body>"
                + "<p>Mleko   2 *\n 2.89 5.78 C</p><div>Chleb<br>4,00</div><table><tr><td>Name</td><td>1 * 2.00 2.00 A</td></tr></table></body></html>";

            var lines = HtmlTextExtractor.ExtractText(html).Split(Environment.NewLine);

            Assert.Equal(new[] { "Mleko 2 * 2.89 5.78 C", "Chleb", "4,00", "Name 1 * 2.00 2.00 A" }, lines);
        }

        [Fact]
        public void ExtractText_ScriptsAndComments_Dropped()
        {
            var html = "<body><!-- <b>hidden</b> --><script>var a = '<pre>x</pre>';</script><pre>Mleko 5,00<!-- also hidden --></pre></body>";

            Assert.Equal("Mleko 5,00", HtmlTextExtractor.ExtractText(html));
        }

        [Fact]
        public void ExtractText_PreText_KeptAsItIs()
        {
            var text = HtmlTextExtractor.ExtractText("<pre>  A&nbsp;B &amp; C   \r\n\r\n\r\n\r\n    2 * 1.00 2.00 A</pre>");

            Assert.Equal("  A B & C" + Environment.NewLine + Environment.NewLine + "    2 * 1.00 2.00 A", text);
        }

        [Fact]
        public void ExtractText_SeveralPreBlocks_OneAfterAnother()
        {
            Assert.Equal("one" + Environment.NewLine + "two", HtmlTextExtractor.ExtractText("<pre>one</pre><p>ignored</p><pre>two</pre>"));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("<html><body></body></html>")]
        [InlineData("<html><head><title>only a title</title></head><body><pre>   \n  </pre></body></html>")]
        public void ExtractText_NoText_Empty(string html)
        {
            Assert.Equal(string.Empty, HtmlTextExtractor.ExtractText(html));
        }

        // ---- the receipt of that page is read like the others

        [Theory]
        [InlineData("Kaucja PET              1 * 0.5 0.5")]
        [InlineData("                     14 * 5.99 83.86 A")]
        public void IsItemRow_LidlRows_True(string line)
        {
            Assert.True(ReceiptRows.IsItemRow(line));
        }

        [Theory]
        [InlineData("Opakowania zwrotne wydania             ")]
        [InlineData("Opakowania zwrotne suma           0,50")]
        [InlineData("PTU A                            95,60")]
        [InlineData("Kwota A 23,00%                   17,88")]
        [InlineData("Suma PLN                155,73")]
        [InlineData("Płatność        Karta płatnicza 156,23")]
        [InlineData("   Taniej za 2                -20,00")]   // a discount, not an item
        public void IsItemRow_LidlOtherLines_False(string line)
        {
            Assert.False(ReceiptRows.IsItemRow(line));
        }

        [Theory]
        [InlineData("   Taniej za 2                -20,00", true)]
        [InlineData("   Lidl Plus voucher           -1,76", true)]
        [InlineData("   Lidl Plus kupon            -12,00", true)]
        [InlineData("-1,76", true)]
        [InlineData("Opakowania zwrotne suma           0,50", false)]
        [InlineData("Suma PLN                155,73", false)]
        [InlineData("Kwota A 23,00%                   17,88", false)]
        [InlineData("--------------------------------------", false)]
        public void IsDiscountRow_LidlLines(string line, bool expected)
        {
            Assert.Equal(expected, ReceiptRows.IsDiscountRow(line));
        }

        [Fact]
        public void Parse_DiscountLinesUnderAnItem_AllTakenOff()
        {
            var items = ReceiptRows.Parse(new[]
            {
                "Szparagi zielon.500g          ",
                "                     2 * 19.99 39.98 C",
                "   Taniej za 2                -20,00",
                "   Lidl Plus voucher           -1,76",
                "Maliny 125g                   ",
                "                     2 * 11.99 23.98 C",
            });

            Assert.Equal(new[] { new ReceiptItem("Szparagi zielon.500g", 18.22), new ReceiptItem("Maliny 125g", 23.98) }, items);
        }

        [Fact]
        public void Parse_NegativeLineNotUnderAnItem_Ignored()
        {
            var items = ReceiptRows.Parse(new[] { "Zniżka -5,00", "Mleko B 1.000 x 5,49 5,49", "Suma kontrolna", "Zniżka -1,00" });

            Assert.Equal(5.49, Assert.Single(items).Amount);
        }

        [Fact]
        public void Parse_RowWithoutTaxLetter_NeedsItsNameOnTheSameLine()
        {
            Assert.Equal(new[] { new ReceiptItem("Kaucja PET", 0.5) }, ReceiptRows.Parse(new[] { "   Kaucja PET              1 * 0.5 0.5" }));
            Assert.Empty(ReceiptRows.Parse(new[] { "1 * 0.5 0.5" }));
        }

        [Fact]
        public void Parse_LidlReceipt_ItemsCostWhatTheReceiptSays()
        {
            var lines = HtmlTextExtractor.ExtractTextFromFile(Sample).Split(Environment.NewLine);

            var items = ReceiptRows.Parse(lines);

            Assert.Equal(12, items.Count);
            Assert.Equal(new[] { 18.22, 10.92, 9.08, 76.51, 8.66, 8.16, 7.29, 7.0, 5.01, 3.64, 1.24, 0.5 }, items.Select(item => item.Amount));
            Assert.Equal("Szparagi zielon.500g", items[0].Name);
            Assert.Equal("Lay's Oven Baked", items[4].Name);
            Assert.Equal("Kaucja PET", items[^1].Name);
            Assert.Equal(156.23, Math.Round(items.Sum(item => item.Amount), 2)); // "Suma 156,23", the card payment
        }

        // ---- the button: Page1VM.LoadFileAsync

        [Fact]
        public async Task LoadFile_HtmlReceipt_TextFilledAndReadWithoutOcr()
        {
            var vm = new Page1VM(-156.23) { CreateOcr = () => throw new InvalidOperationException("an HTML page needs no OCR") };

            await vm.LoadFileAsync(Sample);
            vm.CalculateCurrentAmount();

            Assert.True(string.IsNullOrEmpty(vm.FileStatus));
            Assert.False(vm.IsFileBusy);
            Assert.Contains("                     2 * 19.99 39.98 C", vm.Text);
            Assert.Equal(12, vm.Amounts.Count);
            Assert.Equal(-156.23, vm.CalculatedAmount);
            Assert.Equal(0.0, vm.Diff);
            Assert.Equal(-1822, vm.Amounts[0].FromAmount);
            Assert.Equal("Szparagi zielon500g", vm.Amounts[0].Note);
            Assert.Equal("Kaucja PET", vm.Amounts[^1].Note);
        }

        [Theory]
        [InlineData("receipt.html")]
        [InlineData("receipt.HTM")]
        [InlineData("receipt.htm")]
        public async Task LoadFile_HtmlExtensions_AllRead(string name)
        {
            var path = Path.Combine(this.dir, name);
            File.WriteAllText(path, "<pre>Mleko B 1.000 x 5,49 5,49</pre>");
            var vm = new Page1VM(-5.49);

            await vm.LoadFileAsync(path);

            Assert.Equal("Mleko B 1.000 x 5,49 5,49", vm.Text);
        }

        [Theory]
        [InlineData("receipt.txt")]
        [InlineData("receipt.TXT")]
        public async Task LoadFile_TextFile_ContentIsTheText(string name)
        {
            var path = Path.Combine(this.dir, name);
            File.WriteAllText(path, "Beer Baltyka 0 50,0 A" + Environment.NewLine + "25.50 A");
            var vm = new Page1VM(-75.5);

            await vm.LoadFileAsync(path);
            vm.CalculateCurrentAmount();

            Assert.Equal(2, vm.Amounts.Count);
            Assert.Equal(-75.5, vm.CalculatedAmount);
        }

        [Fact]
        public async Task LoadFile_UnsupportedFile_TextKeptAndStatusSaysSo()
        {
            var path = Path.Combine(this.dir, "receipt.docx");
            File.WriteAllText(path, "whatever");
            var vm = new Page1VM(-1.0) { Text = "old text" };

            await vm.LoadFileAsync(path);

            Assert.Equal("old text", vm.Text);
            Assert.Contains(".docx", vm.FileStatus);
            Assert.False(vm.IsFileBusy);
        }

        [Fact]
        public async Task LoadFile_HtmlWithoutText_TextKeptAndStatusSaysNoText()
        {
            var path = Path.Combine(this.dir, "empty.html");
            File.WriteAllText(path, "<html><body></body></html>");
            var vm = new Page1VM(-1.0) { Text = "old text" };

            await vm.LoadFileAsync(path);

            Assert.Equal("old text", vm.Text);
            Assert.Equal(LocalizationService.Instance["file_no_text"], vm.FileStatus);
        }

        [Fact]
        public async Task LoadFile_FileMissing_StatusHasTheReason()
        {
            var vm = new Page1VM(-1.0) { Text = "old text" };

            await vm.LoadFileAsync(Path.Combine(this.dir, "nothing.html"));

            Assert.Equal("old text", vm.Text);
            Assert.False(string.IsNullOrWhiteSpace(vm.FileStatus));
            Assert.False(vm.IsFileBusy);
        }

        [Fact]
        public async Task LoadFile_AfterAFailure_StatusClearedByAGoodFile()
        {
            var vm = new Page1VM(-5.49);
            await vm.LoadFileAsync(Path.Combine(this.dir, "nothing.html"));
            Assert.False(string.IsNullOrEmpty(vm.FileStatus));
            var path = Path.Combine(this.dir, "good.html");
            File.WriteAllText(path, "<pre>5,49 A</pre>");

            await vm.LoadFileAsync(path);

            Assert.True(string.IsNullOrEmpty(vm.FileStatus));
            Assert.Equal("5,49 A", vm.Text);
        }

        // ---- ReceiptFiles

        [Theory]
        [InlineData("a.pdf", true)]
        [InlineData("a.PDF", true)]
        [InlineData("a.html", true)]
        [InlineData("a.htm", true)]
        [InlineData("a.txt", true)]
        [InlineData("a.docx", false)]
        [InlineData("a.png", false)]
        [InlineData("a", false)]
        public void IsSupported_ByExtension(string path, bool expected)
        {
            Assert.Equal(expected, ReceiptFiles.IsSupported(path));
        }

        [Fact]
        public void Patterns_OneForEveryExtension()
        {
            Assert.Equal(new[] { "*.pdf", "*.html", "*.htm", "*.txt", "*.text" }, ReceiptFiles.Patterns);
        }

        [Fact]
        public void ReadText_Pdf_NotItsJob()
        {
            Assert.Throws<NotSupportedException>(() => ReceiptFiles.ReadText("a.pdf"));
        }
    }
}
