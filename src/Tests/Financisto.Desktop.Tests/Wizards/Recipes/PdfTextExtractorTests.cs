namespace Financisto.Desktop.Tests.Wizards.Recipes
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Wizards.RecipesWizard.ViewModel;
    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.Core;
    using UglyToad.PdfPig.Fonts.Standard14Fonts;
    using UglyToad.PdfPig.Writer;
    using Xunit;

    public sealed class PdfTextExtractorTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "Financisto.Desktop.Tests", "pdf", Guid.NewGuid().ToString("N"));

        public PdfTextExtractorTests()
        {
            Directory.CreateDirectory(this.dir);
        }

        public void Dispose()
        {
            Directory.Delete(this.dir, true);
        }

        [Fact]
        public void ExtractText_TextPdf_LinesInReadingOrder()
        {
            // drawn out of order (the lower line first, the amount before its name): the text must still read top to bottom, left to right
            var path = this.CreatePdf(page =>
            {
                var font = page.Font;
                page.Page.AddText("Milk 25.00 A", 12, new PdfPoint(25, 680), font);
                page.Page.AddText("A", 12, new PdfPoint(300, 700), font);
                page.Page.AddText("50.00", 12, new PdfPoint(250, 700), font);
                page.Page.AddText("Beer Baltyka", 12, new PdfPoint(25, 699), font);
            });

            var text = PdfTextExtractor.ExtractText(path);

            Assert.Equal("Beer Baltyka 50.00 A" + Environment.NewLine + "Milk 25.00 A", text);
        }

        [Fact]
        public void ExtractText_SeveralPages_TextOfEveryPage()
        {
            var path = this.CreatePdf(
                page => page.Page.AddText("Page one", 12, new PdfPoint(25, 700), page.Font),
                page => page.Page.AddText("Page two", 12, new PdfPoint(25, 700), page.Font));

            var text = PdfTextExtractor.ExtractText(path);

            Assert.Equal("Page one" + Environment.NewLine + "Page two", text);
        }

        [Fact]
        public void ExtractText_PdfWithoutText_Empty()
        {
            var path = this.CreatePdf(_ => { });

            Assert.Equal(string.Empty, PdfTextExtractor.ExtractText(path));
        }

        [Fact]
        public void ExtractText_NotAPdf_Throws()
        {
            var path = Path.Combine(this.dir, "notes.pdf");
            File.WriteAllText(path, "just some text");

            Assert.ThrowsAny<Exception>(() => PdfTextExtractor.ExtractText(path));
        }

        [Fact]
        public async Task LoadFileAsync_TextPdf_TextFieldFilled()
        {
            var path = this.CreatePdf(page => page.Page.AddText("Beer Baltyka 50.00 A", 12, new PdfPoint(25, 700), page.Font));
            var vm = new Page1VM(-50.0) { Text = "old text" };

            await vm.LoadFileAsync(path);

            Assert.Equal("Beer Baltyka 50.00 A", vm.Text);
            Assert.True(string.IsNullOrEmpty(vm.FileStatus));
            Assert.True(vm.IsValid());
        }

        [Fact]
        public async Task LoadFileAsync_PdfWithoutText_TextKeptAndStatusSet()
        {
            var path = this.CreatePdf(_ => { });
            var vm = new Page1VM(-50.0) { Text = "old text" };

            await vm.LoadFileAsync(path);

            Assert.Equal("old text", vm.Text);
            Assert.False(string.IsNullOrWhiteSpace(vm.FileStatus));
        }

        [Fact]
        public async Task LoadFileAsync_BadFile_TextKeptAndStatusSet()
        {
            var path = Path.Combine(this.dir, "notes.pdf");
            File.WriteAllText(path, "just some text");
            var vm = new Page1VM(-50.0) { Text = "old text" };

            await vm.LoadFileAsync(path);

            Assert.Equal("old text", vm.Text);
            Assert.False(string.IsNullOrWhiteSpace(vm.FileStatus));
        }

        [Fact]
        public async Task LoadFileAsync_AfterFailure_StatusClearedByGoodPdf()
        {
            var vm = new Page1VM(-50.0);
            await vm.LoadFileAsync(this.CreatePdf(_ => { }));
            Assert.False(string.IsNullOrWhiteSpace(vm.FileStatus));

            await vm.LoadFileAsync(this.CreatePdf(page => page.Page.AddText("25.50 A", 12, new PdfPoint(25, 700), page.Font)));

            Assert.True(string.IsNullOrEmpty(vm.FileStatus));
            Assert.Equal("25.50 A", vm.Text);
        }

        private string CreatePdf(params Action<(PdfPageBuilder Page, PdfDocumentBuilder.AddedFont Font)>[] pages)
        {
            var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            foreach (var fill in pages)
            {
                fill((builder.AddPage(PageSize.A4), font));
            }

            var path = Path.Combine(this.dir, Guid.NewGuid().ToString("N") + ".pdf");
            File.WriteAllBytes(path, builder.Build());
            return path;
        }
    }
}
