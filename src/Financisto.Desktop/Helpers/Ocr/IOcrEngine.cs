using System.Threading.Tasks;

namespace Financisto.Desktop.Helpers.Ocr;

/// <summary>Text recognition for a picture of a document (a scanned receipt).</summary>
public interface IOcrEngine
{
    /// <summary>The text of an encoded image (JPEG, PNG...): one line per row of the picture; empty when nothing was recognized.</summary>
    Task<string> RecognizeAsync(byte[] image);
}
