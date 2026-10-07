namespace Financisto.Desktop.Tests
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using Avalonia.Threading;
    using Financisto.Common.Entities;

    internal static class TestEnvironment
    {
        /// <summary>
        /// SettingsService.Current is a static singleton that saves to disk.
        /// Point it at a scratch file before anything touches it so tests never read or overwrite the real one.
        /// </summary>
        [ModuleInitializer]
        internal static void IsolateSettings()
        {
            var dir = Path.Combine(Path.GetTempPath(), "Financisto.Desktop.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            Environment.SetEnvironmentVariable("FINANCISTO_SETTINGS_PATH", Path.Combine(dir, "Settings.dat"));
        }

        /// <summary>
        /// MainWindowVM hands page changes to Dispatcher.UIThread, which is bound to the first thread that touches it and
        /// never runs anything unless that thread pumps it. Give it a dedicated thread for the whole test run.
        /// </summary>
        [ModuleInitializer]
        internal static void StartUiThread()
        {
            var started = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                var dispatcher = Dispatcher.UIThread;
                started.Set();
                dispatcher.MainLoop(CancellationToken.None);
            })
            {
                IsBackground = true,
                Name = "Avalonia UI (tests)",
            };
            thread.Start();
            started.Wait();
        }
    }
}
