using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Utils;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Helpers.Ocr;
using Financisto.Desktop.Wizards.RecipesWizard.View;
using Prism.Commands;

namespace Financisto.Desktop.Wizards.RecipesWizard.ViewModel
{
    public class Page1VM : RecipesWizardPageVMBase
    {
        //TODO fix '100600 Балтика' case then it calculates (100600 Б), no more symbols after a|b
        private readonly string pattern = RecipesFormatter.Pattern + @"(\t|\n|\r|$)";
        private static readonly char[] charactersToRemove = { ':', '/', '?', '#', '[', ']', '@', '*', '.', ',', '\"', '&', '\'' };
        private DelegateCommand _highlightCommand;
        private string text;
        private string? pdfStatus;
        private bool isPdfBusy;
        private int rememberedHits;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly RecipeCategoryMemory? memory;

        /// <param name="memory">The categories chosen for these lines on earlier receipts; null = nothing remembered.</param>
        public Page1VM(double totalAmount, RecipeCategoryMemory? memory = null)
        {
            TotalAmount = totalAmount;
            this.memory = memory;
        }

        public List<FinancistoTransactionDto> Amounts { get; } = new List<FinancistoTransactionDto>();

        public DelegateCommand HighlightCommand => _highlightCommand ??= new DelegateCommand(HighLight);

        public string Text
        {
            get => this.text;
            set
            {
                this.text = value;
                this.RaisePropertyChanged(nameof(this.Text));
            }
        }

        /// <summary>Where the OCR engine for scanned PDFs comes from; <c>null</c> (no OCR on this system) goes without it. A test sets its own.</summary>
        internal Func<IOcrEngine?> CreateOcr { get; set; } = OcrEngines.GetDefault;

        /// <summary>A PDF is being read (OCR of a scan takes a few seconds).</summary>
        public bool IsPdfBusy
        {
            get => this.isPdfBusy;
            private set
            {
                this.isPdfBusy = value;
                this.RaisePropertyChanged(nameof(this.IsPdfBusy));
                this.RaisePropertyChanged(nameof(this.IsPdfIdle));
            }
        }

        public bool IsPdfIdle => !this.IsPdfBusy;

        /// <summary>Why the last PDF gave no text (a scan without OCR, a damaged file); empty when it was read.</summary>
        public string? PdfStatus
        {
            get => this.pdfStatus;
            private set
            {
                this.pdfStatus = value;
                this.RaisePropertyChanged(nameof(this.PdfStatus));
            }
        }

        public override string Title => "Paste text";

        /// <summary>
        /// Puts the text of the PDF into <see cref="Text"/> (replacing it): its text layer, or, for a scan, what the OCR of this system reads from its pictures.
        /// A PDF without text, a bad file or a failed OCR leaves <see cref="Text"/> alone and sets <see cref="PdfStatus"/>.
        /// </summary>
        public async Task LoadPdfAsync(string filePath)
        {
            IsPdfBusy = true;
            PdfStatus = null;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Logger.Info($"[Recipes] reading PDF {System.IO.Path.GetFileName(filePath)}, {new System.IO.FileInfo(filePath).Length} bytes");
                var (extracted, ocrTried) = await Task.Run(() => ReadPdfAsync(filePath));
                Logger.Info($"[Recipes] PDF read in {stopwatch.ElapsedMilliseconds} ms: {extracted.Length} characters, OCR used: {ocrTried}");
                if (string.IsNullOrWhiteSpace(extracted))
                {
                    PdfStatus = LocalizationService.Instance[ocrTried ? "pdf_ocr_empty" : "pdf_no_text"];
                    return;
                }

                Text = extracted;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"[Recipes] the PDF {System.IO.Path.GetFileName(filePath)} could not be read");
                PdfStatus = string.Format(LocalizationService.Instance["pdf_read_error"], ex.Message);
            }
            finally
            {
                IsPdfBusy = false;
            }
        }

        private async Task<(string text, bool ocrTried)> ReadPdfAsync(string filePath)
        {
            var text = PdfTextExtractor.ExtractText(filePath);
            if (!string.IsNullOrWhiteSpace(text))
            {
                Logger.Info($"[Recipes] the PDF has a text layer ({text.Length} characters)");
                return (text, false);
            }

            // No text layer: a scan. Read its pictures with the OCR of this system, if it has one.
            var ocr = CreateOcr();
            if (ocr is null)
            {
                Logger.Info("[Recipes] the PDF has no text layer and this system has no OCR");
                return (string.Empty, false);
            }

            var images = PdfTextExtractor.ExtractImages(filePath);
            Logger.Info($"[Recipes] the PDF has no text layer: OCR of {images.Count} picture(s)");
            var pages = new List<string>();
            foreach (var image in images)
            {
                pages.Add(await ocr.RecognizeAsync(image));
            }

            return (string.Join(Environment.NewLine, pages.Where(page => !string.IsNullOrWhiteSpace(page))), true);
        }

        public void CalculateCurrentAmount()
        {
            Amounts.Clear();
            rememberedHits = 0;

            if (!string.IsNullOrEmpty(text))
            {
                double tmp = 0.0;
                int order = 1;
                var lines = RecipiesHelper.SplitLines(text).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
                foreach (var (amount, note) in ReadAmounts(lines))
                {
                    tmp += amount;

                    if (DoubleUtils.DoubleNotEqual(amount, 0.0))
                    {
                        var transaction = new FinancistoTransactionDto
                        {
                            FromAmount = Convert.ToInt64(amount * -100.0),
                            Order = order++
                        };
                        ParseLine(note, transaction);
                        Amounts.Add(transaction);
                    }
                }

                // cents: a sum of doubles is off by 1e-13 (Diff would print as -0.00)
                CalculatedAmount = Math.Round(Math.Abs(tmp), 2) * -1.0;
                Logger.Info($"[Recipes] {lines.Count} lines -> {Amounts.Count} rows, {rememberedHits} categories from the remembered ones, sum {CalculatedAmount:F2}");
            }
        }

        // The amount and the text around it of every receipt row: a receipt table (rows with quantity x price, Rabat lines) or lines with a marked amount ("12,50 A")
        private IEnumerable<(double amount, string note)> ReadAmounts(List<string> lines)
        {
            if (ReceiptRows.IsReceiptTable(lines))
            {
                return ReceiptRows.Parse(lines).Select(item => (item.Amount, item.Name));
            }

            return ReadMarkedAmounts(lines);
        }

        private IEnumerable<(double amount, string note)> ReadMarkedAmounts(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                var findNumber = Regex.Match(line, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(1000));
                if (findNumber.Success)
                {
                    var amount = GetDouble(findNumber.Value.Substring(0, findNumber.Value.Length - 2).Replace(",", ".").Trim());
                    yield return (amount, line.Replace(findNumber.Value, string.Empty));
                }
            }
        }

        public override bool IsValid() => !string.IsNullOrWhiteSpace(Text);

        private static double GetDouble(string value, double defaultValue = 0.0)
        {
            double result;
            if (!double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result) &&
                !double.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("en-US"), out result) &&
                !double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            {
                result = defaultValue;
            }
            return result;
        }

        private void HighLight()
        {
            Text = RecipiesHelper.FormatText(Text);
            CalculateCurrentAmount();
        }

        private static bool ContainsString(string title, string[] description)
        {
            if (!string.IsNullOrEmpty(title) && description != null)
            {
                return description.Any(x => title.Contains(x, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        /// <summary>
        /// Gives the row its category. What the user chose for the same note on an earlier receipt (<see cref="RecipeCategoryMemory"/>) comes first;
        /// a note never seen is matched against the words of the category titles.
        /// </summary>
        /// <param name="transaction">The row; its <c>Note</c> is already the cleaned text.</param>
        /// <param name="words">The words of the cleaned text.</param>
        private void TryParseCategory(FinancistoTransactionDto transaction, string[] words)
        {
            if (memory is not null && memory.TryGetCategoryId(transaction.Note, out var remembered))
            {
                transaction.CategoryId = remembered;
                rememberedHits++;
                return;
            }

            var category = DbManual.Category
                    .Where(x => x.Id > 0)
                    .FirstOrDefault(l => ContainsString(l.Title, words));
            transaction.CategoryId = category?.Id ?? 0;
        }

        private void ParseLine(string line, FinancistoTransactionDto transaction)
        {
            var note = line;
            string[] words = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(note))
            {
                foreach (char c in charactersToRemove)
                {
                    note = note.Replace(c.ToString(), string.Empty);
                }

                words = note.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                              .Where(x => x.Length > 2)
                              .Select(x => x.Trim('-').Trim())
                              .ToArray();

                note = string.Join(" ", words);
            }
            transaction.Note = string.IsNullOrWhiteSpace(note) ? string.Empty : note.TrimEnd();

            if (!string.IsNullOrWhiteSpace(line))
            {
                TryParseCategory(transaction, words);
            }
        }
    }
}
