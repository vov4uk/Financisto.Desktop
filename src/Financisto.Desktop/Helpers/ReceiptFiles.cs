using System;
using System.IO;
using System.Linq;

namespace Financisto.Desktop.Helpers;

/// <summary>The kinds of file the receipt page can open, and how the ones that are plain text are read (a PDF needs the text layer or OCR: see the page).</summary>
public static class ReceiptFiles
{
    public static readonly string[] PdfExtensions = { ".pdf" };

    /// <summary>E-receipts saved as a web page (Lidl and others).</summary>
    public static readonly string[] HtmlExtensions = { ".html", ".htm" };

    public static readonly string[] TextExtensions = { ".txt", ".text" };

    public static bool IsPdf(string path) => HasExtension(path, PdfExtensions);

    public static bool IsHtml(string path) => HasExtension(path, HtmlExtensions);

    public static bool IsText(string path) => HasExtension(path, TextExtensions);

    public static bool IsSupported(string path) => IsPdf(path) || IsHtml(path) || IsText(path);

    /// <summary>The picker patterns of all the supported files ("*.pdf", "*.html"...).</summary>
    public static string[] Patterns => PdfExtensions.Concat(HtmlExtensions).Concat(TextExtensions).Select(extension => "*" + extension).ToArray();

    /// <summary>The text of an HTML or a plain text file; throws <see cref="NotSupportedException"/> for any other (a PDF is read by the page).</summary>
    public static string ReadText(string path)
    {
        if (IsHtml(path))
        {
            return HtmlTextExtractor.ExtractTextFromFile(path);
        }

        if (IsText(path))
        {
            return File.ReadAllText(path);
        }

        throw new NotSupportedException($"{Path.GetExtension(path)} files are not supported: open a PDF, HTML or text file");
    }

    private static bool HasExtension(string path, string[] extensions) => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
