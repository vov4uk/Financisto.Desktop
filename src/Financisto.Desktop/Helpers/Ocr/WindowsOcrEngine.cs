using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;

namespace Financisto.Desktop.Helpers.Ocr;

/// <summary>
/// The OCR built into Windows, reached through <c>Financisto.Ocr.Windows.dll</c> (src/Ocr): it needs a windows target framework, so it is
/// a separate DLL in <c>&lt;app&gt;/ocr</c> that exists only in the Windows builds. Everywhere else (and when the folder is missing)
/// <see cref="TryCreate"/> gives <c>null</c> and the caller goes without OCR.
/// </summary>
internal sealed class WindowsOcrEngine : IOcrEngine
{
    private const string AssemblyFile = "Financisto.Ocr.Windows.dll";
    private const string TypeName = "Financisto.Ocr.Windows.WindowsOcr";
    private const string MethodName = "RecognizeAsync";

    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private readonly Func<byte[], Task<string>> recognize;

    private WindowsOcrEngine(Func<byte[], Task<string>> recognize)
    {
        this.recognize = recognize;
    }

    /// <summary>The engine from the DLL in <paramref name="folder"/>, or <c>null</c> (not Windows, no DLL, a DLL that doesn't fit).</summary>
    public static WindowsOcrEngine? TryCreate(string folder)
    {
        var path = Path.Combine(folder, AssemblyFile);
        if (!OperatingSystem.IsWindows() || !File.Exists(path))
        {
            Logger.Info($"[OCR] Windows OCR is off: {(OperatingSystem.IsWindows() ? $"{path} is missing" : "this is not Windows")}");
            return null;
        }

        try
        {
            // The DLL needs the Windows API projection and WinRT.Runtime, which sit next to it
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                var dependency = Path.Combine(folder, name.Name + ".dll");
                return File.Exists(dependency) ? context.LoadFromAssemblyPath(dependency) : null;
            };

            var method = AssemblyLoadContext.Default.LoadFromAssemblyPath(path).GetType(TypeName, throwOnError: true)!
                .GetMethod(MethodName, BindingFlags.Public | BindingFlags.Static, new[] { typeof(byte[]) });
            if (method is null || method.ReturnType != typeof(Task<string>))
            {
                Logger.Error($"{path} has no {TypeName}.{MethodName}(byte[]) returning Task<string>, OCR is off");
                return null;
            }

            Logger.Info($"[OCR] Windows OCR library loaded from {path}");
            return new WindowsOcrEngine(method.CreateDelegate<Func<byte[], Task<string>>>());
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"The OCR library {path} could not be loaded, OCR is off");
            return null;
        }
    }

    public async Task<string> RecognizeAsync(byte[] image)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Logger.Info($"[OCR] recognizing a picture of {image.Length} bytes");
        try
        {
            var text = await this.recognize(image);
            Logger.Info($"[OCR] recognized {text.Length} characters in {stopwatch.ElapsedMilliseconds} ms");
            return text;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"[OCR] failed after {stopwatch.ElapsedMilliseconds} ms");
            throw;
        }
    }
}
