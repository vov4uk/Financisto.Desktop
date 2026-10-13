using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Financisto.Ocr.Windows;

/// <summary>
/// Recognizes the text of an image with the Windows OCR engine. The app finds this class by name (reflection), so
/// <c>Financisto.Ocr.Windows.WindowsOcr.RecognizeAsync(byte[])</c> returning <c>Task&lt;string&gt;</c> is the contract; keep it in step with
/// <c>WindowsOcrEngine</c> in Financisto.Desktop.
/// </summary>
public static class WindowsOcr
{
    /// <summary>
    /// The text of a JPEG/PNG/BMP... image, one line per row of the picture (a receipt row with its columns stays one line).
    /// Uses the OCR languages of the user's profile, or any installed OCR language when none of them has one.
    /// </summary>
    public static async Task<string> RecognizeAsync(byte[] image)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? OcrEngine.AvailableRecognizerLanguages.Select(OcrEngine.TryCreateFromLanguage).FirstOrDefault(e => e != null)
            ?? throw new InvalidOperationException("Windows has no OCR language installed (Settings > Time & language > Language & region).");

        using var bitmap = await DecodeAsync(image, OcrEngine.MaxImageDimension);
        var result = await engine.RecognizeAsync(bitmap);

        return string.Join(Environment.NewLine, GetRows(result));
    }

    private static async Task<SoftwareBitmap> DecodeAsync(byte[] image, uint maxDimension)
    {
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(image);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);

        // The engine refuses a picture over MaxImageDimension on a side: shrink it, keeping the proportions
        var scale = Math.Min(1.0, (double)maxDimension / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        if (scale >= 1.0)
        {
            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }

        var transform = new BitmapTransform
        {
            ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)),
            ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale)),
        };
        return await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform, ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
    }

    // The engine may cut a table row into several "lines" (one per column when the gaps are wide): put the words back
    // together by their vertical position, then read every row left to right.
    private static IEnumerable<string> GetRows(OcrResult result)
    {
        var words = result.Lines.SelectMany(line => line.Words).OrderBy(Middle).ToList();

        var row = new List<OcrWord>();
        double rowMiddle = 0;
        foreach (var word in words)
        {
            // Words of one row are within half a word height of each other; the next row is at least a line away
            if (row.Count > 0 && Middle(word) - rowMiddle > word.BoundingRect.Height / 2)
            {
                yield return Join(row);
                row.Clear();
            }

            if (row.Count == 0)
            {
                rowMiddle = Middle(word);
            }

            row.Add(word);
        }

        if (row.Count > 0)
        {
            yield return Join(row);
        }
    }

    private static double Middle(OcrWord word) => word.BoundingRect.Y + (word.BoundingRect.Height / 2);

    private static string Join(List<OcrWord> row) => string.Join(' ', row.OrderBy(word => word.BoundingRect.X).Select(word => word.Text));
}
