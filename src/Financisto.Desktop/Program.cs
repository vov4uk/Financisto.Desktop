using Avalonia;
using System;
using Financisto.Desktop.Helpers;

namespace Financisto.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        CrashDiagnostics.Install();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            NLog.LogManager.GetCurrentClassLogger().Fatal(ex, "The app failed");
            NLog.LogManager.Flush(TimeSpan.FromSeconds(3));
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .WithDataAnnotationsValidation()
            .LogToTrace();
}
