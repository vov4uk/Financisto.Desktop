using System;
using System.IO;

namespace Financisto.Desktop
{
    public partial class StartOptions
    {
        public required string SettingsPath { get; init; }

        /// <summary>The folder with the bank helper plugins (DLLs); see <see cref="Helpers.BankHelper.PluginBankHelperProvider"/>.</summary>
        public required string PluginsPath { get; init; }

        /// <summary>The folder with the Windows OCR library (Windows builds only); see <see cref="Helpers.Ocr.OcrEngines"/>.</summary>
        public required string OcrPath { get; init; }
    }

    public partial class StartOptions
    {
        public static StartOptions Current { get; } =
            new()
            {
                SettingsPath =
                    Environment.GetEnvironmentVariable("FINANCISTO_SETTINGS_PATH") is { } path
                    && !string.IsNullOrWhiteSpace(path)
                        ? Path.EndsInDirectorySeparator(path) || Directory.Exists(path)
                            ? Path.Combine(path, "Settings.dat")
                            : path
                        : Path.Combine(AppContext.BaseDirectory, "Settings.dat"),
                PluginsPath =
                    Environment.GetEnvironmentVariable("FINANCISTO_PLUGINS_PATH") is { } pluginsPath
                    && !string.IsNullOrWhiteSpace(pluginsPath)
                        ? pluginsPath
                        : Path.Combine(AppContext.BaseDirectory, "plugins"),
                OcrPath =
                    Environment.GetEnvironmentVariable("FINANCISTO_OCR_PATH") is { } ocrPath
                    && !string.IsNullOrWhiteSpace(ocrPath)
                        ? ocrPath
                        : Path.Combine(AppContext.BaseDirectory, "ocr")
            };
    }
}
