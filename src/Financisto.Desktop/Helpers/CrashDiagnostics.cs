using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Financisto.Desktop.Helpers;

/// <summary>
/// What goes to the log when the app dies. A managed exception that nothing catches is logged (and flushed) before the process ends; a native crash
/// (heap corruption in Skia/ANGLE, a driver) can't be logged by the process itself, so the log gets the versions of the native pieces at start and
/// the wizards log their steps, which shows how far the app got. Windows keeps the rest: enable WER LocalDumps to get a dump of such a crash.
/// </summary>
public static class CrashDiagnostics
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    // The libraries a rendering crash or a version mix-up involves: managed side (assemblies) and native side (DLLs of the process)
    private static readonly string[] Assemblies = { "Avalonia.Base", "Avalonia.Skia", "SkiaSharp", "SkiaSharp.HarfBuzz", "HarfBuzzSharp", "LiveChartsCore", "LiveChartsCore.SkiaSharpView", "Microsoft.Data.Sqlite", "UglyToad.PdfPig" };
    private static readonly string[] NativeModules = { "libSkiaSharp", "libHarfBuzzSharp", "av_libglesv2", "libegl", "e_sqlite3", "d3d11", "igd10um64xe", "nvwgf2umx", "atidxx64", "amdxx64" };

    private static int installed;

    /// <summary>Hooks the handlers for exceptions nothing else catches; safe to call more than once.</summary>
    public static void Install()
    {
        if (System.Threading.Interlocked.Exchange(ref installed, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Logger.Fatal(e.ExceptionObject as Exception, $"Unhandled exception (terminating: {e.IsTerminating}): {e.ExceptionObject}");
            NLog.LogManager.Flush(TimeSpan.FromSeconds(3));
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Error(e.Exception, "A task failed and nobody awaited it");
            e.SetObserved();
        };
    }

    /// <summary>One line per piece: OS, runtime, and the version of every library in <see cref="Assemblies"/> that is loaded.</summary>
    public static IReadOnlyList<string> DescribeEnvironment()
    {
        var lines = new List<string>
        {
            $"Environment: {RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture}, {RuntimeInformation.FrameworkDescription}, process {RuntimeInformation.ProcessArchitecture}, id {Environment.ProcessId}",
            $"Folder: {AppContext.BaseDirectory}",
        };

        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName())
            .Where(name => Assemblies.Contains(name.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(name => name.Name);
        lines.AddRange(loaded.Select(name => $"Library: {name.Name} {name.Version}"));
        return lines;
    }

    /// <summary>The native DLLs of the process that matter for rendering and data (with file versions): a Skia generation mix shows here.</summary>
    public static IReadOnlyList<string> DescribeNativeModules()
    {
        var lines = new List<string>();
        try
        {
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                var file = System.IO.Path.GetFileNameWithoutExtension(module.ModuleName);
                if (NativeModules.Contains(file, StringComparer.OrdinalIgnoreCase))
                {
                    lines.Add($"Native: {module.ModuleName} {module.FileVersionInfo.FileVersion} ({module.FileName})");
                }
            }
        }
        catch (Exception ex)
        {
            lines.Add($"Native modules could not be listed: {ex.Message}");
        }

        return lines;
    }

    /// <summary>Writes <see cref="DescribeEnvironment"/> and <see cref="DescribeNativeModules"/> to the log.</summary>
    public static void LogEnvironment()
    {
        foreach (var line in DescribeEnvironment().Concat(DescribeNativeModules()))
        {
            Logger.Info(line);
        }
    }
}
