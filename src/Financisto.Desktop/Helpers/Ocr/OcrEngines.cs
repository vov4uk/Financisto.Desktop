using System;

namespace Financisto.Desktop.Helpers.Ocr;

/// <summary>The OCR engine of this machine, found once: <c>null</c> when there isn't one (any system but Windows).</summary>
public static class OcrEngines
{
    private static readonly Lazy<IOcrEngine?> Default = new(() => WindowsOcrEngine.TryCreate(StartOptions.Current.OcrPath));

    public static IOcrEngine? GetDefault() => Default.Value;
}
