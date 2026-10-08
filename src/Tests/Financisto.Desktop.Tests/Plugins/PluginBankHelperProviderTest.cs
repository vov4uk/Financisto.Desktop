namespace Financisto.Desktop.Tests.Plugins
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.Loader;
    using Financisto.BankHelpers;
    using Financisto.Desktop.Helpers.BankHelper;
    using Xunit;

    /// <summary>
    /// The bank helpers are plugins: single DLLs in a plugins folder. These tests copy the built plugin DLLs, and nothing else,
    /// into a scratch folder, so they also prove that a plugin carries its own dependencies (CsvHelper, MiniExcel, PdfPig, ...).
    /// </summary>
    public sealed class PluginBankHelperProviderTest : IDisposable
    {
        private static readonly string[] PluginNames = { "ABank", "Erste", "Monobank", "Pireus", "Pko", "Privat", "Pumb", "Revolut" };

        // A loaded plugin keeps its DLL locked until the process ends, so a test cannot delete its own folder.
        // All of them live under one root, which the next run clears before it starts, so the temp folder does not keep growing.
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "financisto-plugin-tests");

        private readonly string pluginsFolder = Path.Combine(Root, Guid.NewGuid().ToString("N"));

        static PluginBankHelperProviderTest()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // leftovers still locked by another running test process; they are cleared by a later run
            }
        }

        public PluginBankHelperProviderTest()
        {
            Directory.CreateDirectory(pluginsFolder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(pluginsFolder, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // a loaded plugin keeps its file open; the next run clears the root folder
            }
        }

        [Fact]
        public void MissingFolder_HasNoHelpers()
        {
            var provider = new PluginBankHelperProvider(Path.Combine(pluginsFolder, "does-not-exist"));

            Assert.Empty(provider.BankHelpers);
        }

        [Fact]
        public void FilesThatAreNotPlugins_AreIgnored()
        {
            File.WriteAllText(Path.Combine(pluginsFolder, "broken.dll"), "this is not an assembly");
            File.WriteAllText(Path.Combine(pluginsFolder, "readme.txt"), "not a dll");
            File.Copy(typeof(IBankHelper).Assembly.Location, Path.Combine(pluginsFolder, "Financisto.BankHelpers.Abstractions.dll"));
            File.Copy(typeof(Xunit.Assert).Assembly.Location, Path.Combine(pluginsFolder, "assert.dll"));

            var provider = new PluginBankHelperProvider(pluginsFolder);

            Assert.Empty(provider.BankHelpers);
        }

        [Fact]
        public void AllPlugins_AppearAsHelpers_WithTitleIconAndReportType()
        {
            CopyPlugins(PluginNames);

            var helpers = new PluginBankHelperProvider(pluginsFolder).BankHelpers;

            var actual = helpers.Select(x => (x.BankTitle, x.ReportType)).OrderBy(x => x.ReportType).ThenBy(x => x.BankTitle, StringComparer.OrdinalIgnoreCase).ToList();
            Assert.Equal(
                new[]
                {
                    ("Erste", ReportType.Csv),
                    ("Monobank", ReportType.Csv),
                    ("Revolut", ReportType.Csv),
                    ("A Bank", ReportType.Xlsx),
                    ("Privat", ReportType.Xlsx),
                    ("A Bank", ReportType.Pdf),
                    ("Pireus", ReportType.Pdf),
                    ("PKO", ReportType.Pdf),
                    ("PUMB", ReportType.Pdf),
                },
                actual);

            foreach (var helper in helpers)
            {
                Assert.True(helper.Icon is { Length: > 8 } icon && icon[0] == 0x89 && icon[1] == 'P' && icon[2] == 'N' && icon[3] == 'G', $"{helper.BankTitle} has no PNG icon");
            }
        }

        [Fact]
        public void Plugin_LoadsInItsOwnContext_AndBringsItsDependencies()
        {
            CopyPlugins("Monobank");

            var helper = Assert.Single(new PluginBankHelperProvider(pluginsFolder).BankHelpers);

            var context = AssemblyLoadContext.GetLoadContext(helper.GetType().Assembly);
            Assert.NotSame(AssemblyLoadContext.Default, context);
            Assert.Equal(typeof(IBankHelper).Assembly, helper.GetType().GetInterface(nameof(IBankHelper))!.Assembly);

            // CsvHelper is not next to the plugin: it comes out of the plugin DLL.
            var rows = helper.ParseReport(Asset("mono.eng.csv")).ToList();
            Assert.NotEmpty(rows);
            Assert.Contains(context!.Assemblies, x => x.GetName().Name == "CsvHelper");
        }

        [Theory]
        [InlineData("Erste", "erste.csv")]
        [InlineData("Revolut", "revolut.en.csv")]
        [InlineData("Privat", "privat.xlsx")]
        [InlineData("Pumb", "pumb.pdf")]
        [InlineData("Pireus", "pireus.pdf")]
        [InlineData("Pko", "pko.pdf")]
        public void SingleDllPlugin_ParsesItsStatement(string plugin, string statement)
        {
            CopyPlugins(plugin);
            var expected = ParseDirectly(plugin, statement);

            var helper = Assert.Single(new PluginBankHelperProvider(pluginsFolder).BankHelpers);
            var actual = helper.ParseReport(Asset(statement)).ToList();

            Assert.NotEmpty(actual);
            Assert.Equal(expected.Count, actual.Count);
            Assert.Equal(expected.Select(x => (x.Date, x.Description, x.CardCurrencyAmount, x.Balance)), actual.Select(x => (x.Date, x.Description, x.CardCurrencyAmount, x.Balance)));
        }

        [Fact]
        public void ABankPlugin_ProvidesBothFormats()
        {
            CopyPlugins("ABank");

            var helpers = new PluginBankHelperProvider(pluginsFolder).BankHelpers;

            Assert.Equal(new[] { ReportType.Xlsx, ReportType.Pdf }, helpers.Select(x => x.ReportType).OrderBy(x => x == ReportType.Pdf));
            Assert.All(helpers, x => Assert.Equal("A Bank", x.BankTitle));
            Assert.NotEmpty(helpers.Single(x => x.ReportType == ReportType.Xlsx).ParseReport(Asset("abank.xlsx")));
            Assert.NotEmpty(helpers.Single(x => x.ReportType == ReportType.Pdf).ParseReport(Asset("abank_3_pages.pdf")));
        }

        [Fact]
        public void Title_FollowsTheUiLanguage()
        {
            CopyPlugins("Monobank", "Erste");
            var helpers = new PluginBankHelperProvider(pluginsFolder).BankHelpers;
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("uk");
                Assert.Equal(new[] { "Erste", "Монобанк" }, helpers.Select(x => x.BankTitle).OrderBy(x => x, StringComparer.Ordinal));

                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
                Assert.Equal(new[] { "Erste", "Monobank" }, helpers.Select(x => x.BankTitle).OrderBy(x => x, StringComparer.Ordinal));
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
            }
        }

        [Fact]
        public void OnlyUsableClasses_BecomeHelpers()
        {
            // This test assembly holds a helper with a title, one without, an abstract one and one that needs constructor arguments.
            var helpers = PluginBankHelperProvider.CreateHelpers(typeof(PluginBankHelperProviderTest).Assembly, "tests");

            var helper = Assert.Single(helpers);
            Assert.IsType<ValidTestHelper>(helper);
        }

        private static string Asset(string name) => Path.Combine(Environment.CurrentDirectory, "Assets", name);

        private void CopyPlugins(params string[] names)
        {
            foreach (var name in names)
            {
                var file = $"Financisto.BankHelpers.{name}.dll";
                File.Copy(Path.Combine(AppContext.BaseDirectory, file), Path.Combine(pluginsFolder, file));
            }
        }

        /// <summary>Runs the plugin's helper from the test's own references (not through the plugin loader).</summary>
        private static List<BankTransaction> ParseDirectly(string plugin, string statement)
        {
            var assembly = System.Reflection.Assembly.Load($"Financisto.BankHelpers.{plugin}");
            var type = assembly.GetExportedTypes().First(x => typeof(IBankHelper).IsAssignableFrom(x) && !x.IsAbstract);
            return ((IBankHelper)Activator.CreateInstance(type)!).ParseReport(Asset(statement)).ToList();
        }
    }

    public sealed class BlankTitleHelper : BankHelperBase
    {
        public override string BankTitle => " ";

        public override ReportType ReportType => ReportType.Csv;

        public override IEnumerable<BankTransaction> ParseReport(string filePath) => Array.Empty<BankTransaction>();
    }

    public abstract class AbstractTestHelper : BankHelperBase
    {
        public override string BankTitle => "Abstract";
    }

    public sealed class NeedsArgumentsHelper : BankHelperBase
    {
        public NeedsArgumentsHelper(string argument)
        {
        }

        public override string BankTitle => "Needs arguments";

        public override ReportType ReportType => ReportType.Csv;

        public override IEnumerable<BankTransaction> ParseReport(string filePath) => Array.Empty<BankTransaction>();
    }

    public sealed class ValidTestHelper : BankHelperBase
    {
        public override string BankTitle => "Test bank";

        public override ReportType ReportType => ReportType.Json;

        public override IEnumerable<BankTransaction> ParseReport(string filePath) => Array.Empty<BankTransaction>();
    }
}
