namespace Financisto.Desktop.Tests.Wizards.Recipes
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Helpers.Ocr;
    using Financisto.Desktop.Wizards.RecipesWizard.ViewModel;
    using SkiaSharp;
    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.Core;
    using UglyToad.PdfPig.Fonts.Standard14Fonts;
    using UglyToad.PdfPig.Writer;
    using Xunit;

    /// <summary>A scanned PDF (only a picture on the page) is read by the OCR of the system.</summary>
    public sealed class PdfOcrTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "Financisto.Desktop.Tests", "ocr", Guid.NewGuid().ToString("N"));

        public PdfOcrTests()
        {
            Directory.CreateDirectory(this.dir);
        }

        public void Dispose()
        {
            Directory.Delete(this.dir, true);
        }

        [Theory]
        [InlineData(SKEncodedImageFormat.Png)]
        [InlineData(SKEncodedImageFormat.Jpeg)]
        public void ExtractImages_ScannedPdf_PicturePerPageInPageOrder(SKEncodedImageFormat format)
        {
            var first = RenderText(format, "page one");
            var second = RenderText(format, "page two");
            var path = this.CreateScan(format, first, second);

            var images = PdfTextExtractor.ExtractImages(path);

            Assert.Equal(2, images.Count);
            Assert.All(images, image => Assert.NotNull(SKBitmap.Decode(image)));
            Assert.NotEqual(images[0], images[1]);
        }

        [Fact]
        public void ExtractImages_PdfWithTextOnly_NoImages()
        {
            Assert.Empty(PdfTextExtractor.ExtractImages(this.CreateTextPdf("text")));
        }

        [Fact]
        public async Task LoadFileAsync_ScannedPdf_TextOfEveryPictureFromOcr()
        {
            var path = this.CreateScan(SKEncodedImageFormat.Jpeg, RenderText(SKEncodedImageFormat.Jpeg, "a"), RenderText(SKEncodedImageFormat.Jpeg, "b"));
            var ocr = new FakeOcr(_ => Task.FromResult("row"));
            var vm = new Page1VM(-1.0) { CreateOcr = () => ocr, Text = "old text" };

            await vm.LoadFileAsync(path);

            Assert.Equal(2, ocr.Calls);
            Assert.Equal("row" + Environment.NewLine + "row", vm.Text);
            Assert.True(string.IsNullOrEmpty(vm.FileStatus));
            Assert.False(vm.IsFileBusy);
            Assert.True(vm.IsFileIdle);
        }

        [Fact]
        public async Task LoadFileAsync_TextPdf_OcrNotUsed()
        {
            var ocr = new FakeOcr(_ => Task.FromResult("from ocr"));
            var vm = new Page1VM(-1.0) { CreateOcr = () => ocr };

            await vm.LoadFileAsync(this.CreateTextPdf("25.50 A"));

            Assert.Equal(0, ocr.Calls);
            Assert.Equal("25.50 A", vm.Text);
        }

        [Fact]
        public async Task LoadFileAsync_ScannedPdfAndNoOcr_StatusAsksForOcr()
        {
            var path = this.CreateScan(SKEncodedImageFormat.Png, RenderText(SKEncodedImageFormat.Png, "a"));
            var vm = new Page1VM(-1.0) { CreateOcr = () => null, Text = "old text" };

            await vm.LoadFileAsync(path);

            Assert.Equal("old text", vm.Text);
            Assert.False(string.IsNullOrWhiteSpace(vm.FileStatus));
            Assert.False(vm.IsFileBusy);

            // the status tells apart "no OCR here" from "OCR found nothing"
            var withOcr = new Page1VM(-1.0) { CreateOcr = () => new FakeOcr(_ => Task.FromResult(" ")) };
            await withOcr.LoadFileAsync(path);
            Assert.False(string.IsNullOrWhiteSpace(withOcr.FileStatus));
            Assert.NotEqual(vm.FileStatus, withOcr.FileStatus);
        }

        [Fact]
        public async Task LoadFileAsync_OcrFails_TextKeptAndStatusHasTheReason()
        {
            var path = this.CreateScan(SKEncodedImageFormat.Png, RenderText(SKEncodedImageFormat.Png, "a"));
            var vm = new Page1VM(-1.0) { CreateOcr = () => new FakeOcr(_ => throw new InvalidOperationException("no OCR language")), Text = "old text" };

            await vm.LoadFileAsync(path);

            Assert.Equal("old text", vm.Text);
            Assert.Contains("no OCR language", vm.FileStatus);
            Assert.False(vm.IsFileBusy);
        }

        [Fact]
        public async Task LoadFileAsync_WhileReading_IsBusy()
        {
            var path = this.CreateScan(SKEncodedImageFormat.Png, RenderText(SKEncodedImageFormat.Png, "a"));
            var release = new TaskCompletionSource<string>();
            var entered = new TaskCompletionSource();
            var vm = new Page1VM(-1.0) { CreateOcr = () => new FakeOcr(_ => { entered.SetResult(); return release.Task; }) };

            var loading = vm.LoadFileAsync(path);
            await entered.Task;

            Assert.True(vm.IsFileBusy);
            Assert.False(vm.IsFileIdle);

            release.SetResult("25.50 A");
            await loading;

            Assert.False(vm.IsFileBusy);
            Assert.Equal("25.50 A", vm.Text);
        }

        /// <summary>The real thing: Windows reads a picture of text. Skipped where there is no Windows OCR (other systems, a Windows without an OCR language).</summary>
        [Fact]
        public async Task WindowsOcrEngine_PictureOfText_Recognized()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "Windows OCR is Windows only");
            var engine = WindowsOcrEngine.TryCreate(Path.Combine(AppContext.BaseDirectory, "ocr"));
            Assert.NotNull(engine);

            string text;
            try
            {
                text = await engine.RecognizeAsync(RenderText(SKEncodedImageFormat.Png, "Milk 25,50", "Bread 12,30"));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("no OCR language"))
            {
                Assert.Skip("This Windows has no OCR language installed");
                return;
            }

            var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            Assert.Contains("25,50", lines[0]);
            Assert.Contains("12,30", lines[1]);
        }

        [Fact]
        public void WindowsOcrEngine_NoLibraryInFolder_Null()
        {
            Assert.Null(WindowsOcrEngine.TryCreate(this.dir));
        }

        private static byte[] RenderText(SKEncodedImageFormat format, params string[] lines)
        {
            using var bitmap = new SKBitmap(900, 120 * lines.Length);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.FromFamilyName("Arial"), 60);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            for (var i = 0; i < lines.Length; i++)
            {
                canvas.DrawText(lines[i], 20, 85 + (120 * i), font, paint);
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(format, 95);
            return data.ToArray();
        }

        private string CreateTextPdf(string text)
        {
            var builder = new PdfDocumentBuilder();
            builder.AddPage(PageSize.A4).AddText(text, 12, new PdfPoint(25, 700), builder.AddStandard14Font(Standard14Font.Helvetica));
            return this.Save(builder);
        }

        private string CreateScan(SKEncodedImageFormat format, params byte[][] pictures)
        {
            var builder = new PdfDocumentBuilder();
            foreach (var picture in pictures)
            {
                var page = builder.AddPage(PageSize.A4);
                var placement = new PdfRectangle(20, 600, 520, 700);
                if (format == SKEncodedImageFormat.Jpeg)
                {
                    page.AddJpeg(picture, placement);
                }
                else
                {
                    page.AddPng(picture, placement);
                }
            }

            return this.Save(builder);
        }

        private string Save(PdfDocumentBuilder builder)
        {
            var path = Path.Combine(this.dir, Guid.NewGuid().ToString("N") + ".pdf");
            File.WriteAllBytes(path, builder.Build());
            return path;
        }

        private sealed class FakeOcr(Func<byte[], Task<string>> recognize) : IOcrEngine
        {
            public int Calls { get; private set; }

            public Task<string> RecognizeAsync(byte[] image)
            {
                this.Calls++;
                return recognize(image);
            }
        }
    }
}
