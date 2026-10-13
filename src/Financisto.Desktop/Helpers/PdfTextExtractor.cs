using System;
using System.Collections.Generic;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Financisto.Desktop.Helpers;

/// <summary>
/// Reads the text layer of a PDF (a receipt, a statement) as plain text with one text line per visual line.
/// A scan or a photo saved as PDF has no text layer: the result is empty (that needs OCR, which PdfPig doesn't do).
/// </summary>
public static class PdfTextExtractor
{
    /// <summary>The text of every page, line by line; empty when the PDF has no text layer. Throws when the file isn't a readable PDF.</summary>
    public static string ExtractText(string filePath)
    {
        using var document = PdfDocument.Open(filePath);

        var lines = document.GetPages().SelectMany(GetLines);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// The pictures of a scanned PDF as JPEG/PNG files, page by page (and top to bottom on a page), to run OCR on.
    /// Pictures PdfPig can't turn into one of those formats are left out.
    /// </summary>
    public static IReadOnlyList<byte[]> ExtractImages(string filePath)
    {
        using var document = PdfDocument.Open(filePath);

        var images = new List<byte[]>();
        foreach (var page in document.GetPages())
        {
            foreach (var image in page.GetImages().OrderByDescending(image => image.Bounds.Top))
            {
                if (image.TryGetPng(out var png))
                {
                    images.Add(png);
                }
                else if (IsJpeg(image.RawBytes))
                {
                    images.Add(image.RawBytes.ToArray()); // a DCT picture is a JPEG file as it is
                }
            }
        }

        return images;
    }

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) => bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8;

    // PdfPig returns the words in content order, which is the order the generator drew them in, not the reading
    // order: group them by baseline (top to bottom), then sort every line left to right.
    private static IEnumerable<string> GetLines(Page page)
    {
        var words = page.GetWords()
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .OrderByDescending(Baseline)
            .ToList();

        var line = new List<Word>();
        double lineBaseline = 0;
        foreach (var word in words)
        {
            // Half the word height: a superscript or a font swap stays on its line, the next line doesn't join it
            if (line.Count > 0 && lineBaseline - Baseline(word) > Math.Max(1, word.BoundingBox.Height / 2))
            {
                yield return Join(line);
                line.Clear();
            }

            if (line.Count == 0)
            {
                lineBaseline = Baseline(word);
            }

            line.Add(word);
        }

        if (line.Count > 0)
        {
            yield return Join(line);
        }
    }

    private static double Baseline(Word word) => word.Letters[0].StartBaseLine.Y;

    private static string Join(List<Word> line) =>
        string.Join(' ', line.OrderBy(word => word.BoundingBox.Left).Select(word => word.Text));
}
