namespace Financisto.Desktop.Tests.Wizards.Recipes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Financisto.Desktop.Wizards;
    using Financisto.Desktop.Wizards.RecipesWizard.ViewModel;
    using Moq;
    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using Xunit;

    /// <summary>
    /// A native crash leaves nothing in the app's log, so the receipt wizard writes down every step (the last line shows how far it got), and a
    /// managed failure in the flow is logged and reported instead of ending the app.
    /// </summary>
    [Collection("NLog configuration")]
    public sealed class WizardDiagnosticsTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "Financisto.Desktop.Tests", "diagnostics", Guid.NewGuid().ToString("N"));
        private readonly LoggingConfiguration previous;
        private readonly MemoryTarget log = new("memory") { Layout = "${level:uppercase=true}|${message}${onexception:|${exception:format=Message}}" };

        public WizardDiagnosticsTests()
        {
            Directory.CreateDirectory(this.dir);
            this.previous = LogManager.Configuration;
            var config = new LoggingConfiguration();
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, this.log);
            LogManager.Configuration = config;

            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel { Id = 7, Title = "Dairy" } });
            DbManual.SetupTests(new List<AccountFilterModel>());
            DbManual.SetupTests(new List<CurrencyModel>());
        }

        public void Dispose()
        {
            LogManager.Configuration = this.previous;
            DbManual.ResetAllDatabaseManuals();
            Directory.Delete(this.dir, true);
        }

        private IReadOnlyList<string> Lines => this.log.Logs.ToList();

        [Fact]
        public void Wizard_FromStartToSave_EveryStepIsInTheLog()
        {
            var path = Path.Combine(this.dir, "RecipeCategories.json");
            File.WriteAllText(path, "{ \"Mleko UHT\": \"Dairy\" }");
            var vm = new RecipesVM(-5.49, new RecipeCategoryMemory(path));
            ((Page1VM)vm.CurrentPage).Text = "Mleko UHT B 1.000 x 5,49 5,49";
            List<TransactionDto> output = null;
            vm.RequestClose += (parts, save) => output = save ? parts as List<TransactionDto> : null;

            vm.MoveNextCommand.Execute();
            vm.MoveNextCommand.Execute();

            Assert.Single(output);
            var lines = this.Lines;
            int At(string text) => lines.ToList().FindIndex(line => line.Contains(text));
            Assert.True(At("[Recipes] 1 remembered categories read from") >= 0, string.Join(Environment.NewLine, lines));
            Assert.True(At("[Recipes] wizard started, total -5") >= 0);
            Assert.True(At("[Recipes] 1 lines -> 1 rows, 1 categories from the remembered ones") >= 0);
            Assert.True(At("[Recipes] page 1 -> 2: 1 rows") >= 0);
            Assert.True(At("[RecipesVM] closing, save=True") >= 0);
            Assert.True(At("[Recipes] saved: 1 rows, 1 with a category, 1 with a note") >= 0);
            Assert.True(At("[Recipes] 1 remembered categories saved to") >= 0);
            Assert.True(At("[RecipesVM] closed") >= 0);

            // in the order they happened: the last line tells where a crash happened
            Assert.True(At("wizard started") < At("page 1 -> 2"));
            Assert.True(At("page 1 -> 2") < At("closing, save=True"));
            Assert.True(At("closing, save=True") < At("saved: 1 rows"));
            Assert.True(At("saved: 1 rows") < At("[RecipesVM] closed"));
        }

        [Fact]
        public void Wizard_Cancelled_LogsThat()
        {
            var vm = new RecipesVM(-5.49, new RecipeCategoryMemory(Path.Combine(this.dir, "x.json")));

            vm.CancelCommand.Execute();

            Assert.Contains(this.Lines, line => line.Contains("[RecipesVM] closing, save=False"));
        }

        [Fact]
        public async Task Pdf_Read_StepsAreInTheLog()
        {
            var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
            builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4).AddText("5.49 A", 12, new UglyToad.PdfPig.Core.PdfPoint(25, 700), builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica));
            var pdf = Path.Combine(this.dir, "receipt.pdf");
            File.WriteAllBytes(pdf, builder.Build());
            var vm = new Page1VM(-5.49);

            await vm.LoadFileAsync(pdf);

            Assert.Contains(this.Lines, line => line.Contains("[Recipes] reading file receipt.pdf"));
            Assert.Contains(this.Lines, line => line.Contains("[Recipes] the PDF has a text layer"));
            Assert.Contains(this.Lines, line => line.Contains("[Recipes] file read in") && line.Contains("OCR used: False"));
        }

        [Fact]
        public async Task Pdf_Unreadable_LoggedAsAnError()
        {
            var pdf = Path.Combine(this.dir, "broken.pdf");
            File.WriteAllText(pdf, "not a pdf");
            var vm = new Page1VM(-1.0);

            await vm.LoadFileAsync(pdf);

            Assert.Contains(this.Lines, line => line.StartsWith("ERROR|") && line.Contains("broken.pdf could not be read"));
            Assert.False(string.IsNullOrEmpty(vm.FileStatus));
        }

        [Fact]
        public void WizardThatCannotMakeItsResult_StaysOpenAndLogsTheError()
        {
            var wizard = new FailingWizard();
            var closed = false;
            wizard.RequestClose += (_, _) => closed = true;

            var exception = Record.Exception(() => wizard.MoveNextCommand.Execute());

            Assert.Null(exception);
            Assert.False(closed);
            Assert.Contains(this.Lines, line => line.StartsWith("ERROR|") && line.Contains("[FailingWizard] its result could not be made") && line.Contains("no result today"));
        }

        [Fact]
        public void WizardThatCannotMakeItsResult_TellsTheUser()
        {
            var wizard = new FailingWizard();
            var changed = new List<string>();
            wizard.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            wizard.MoveNextCommand.Execute();

            Assert.Contains("no result today", wizard.ErrorMessage);
            Assert.Contains(nameof(WizardBaseVM.ErrorMessage), changed);
        }

        [Fact]
        public void WizardErrorMessage_ClearedByTheNextTry()
        {
            var wizard = new FailingWizard();
            object result = null;
            wizard.RequestClose += (output, _) => result = output;
            wizard.MoveNextCommand.Execute();
            Assert.NotNull(wizard.ErrorMessage);

            wizard.Fail = false;
            wizard.MoveNextCommand.Execute();

            Assert.Null(wizard.ErrorMessage);
            Assert.Equal("result", result);
        }

        [Fact]
        public void WizardErrorMessage_ClearedByAPageChange()
        {
            var wizard = new FailingWizard(pages: 2);
            wizard.MoveNextCommand.Execute();   // to the second page
            wizard.MoveNextCommand.Execute();   // finish: fails
            Assert.NotNull(wizard.ErrorMessage);

            wizard.MovePreviousCommand.Execute();

            Assert.Null(wizard.ErrorMessage);
        }

        [Fact]
        public void WizardErrorMessage_NoneWhenTheWizardFinishes()
        {
            var wizard = new FailingWizard { Fail = false };

            wizard.MoveNextCommand.Execute();

            Assert.Null(wizard.ErrorMessage);
        }

        [Fact]
        public async Task RecipesButton_WizardThrows_MessageShownNothingAddedAndLogged()
        {
            var dialog = new Mock<IDialogWrapper>(MockBehavior.Strict);
            dialog.Setup(d => d.ShowWizardAsync(It.IsAny<RecipesVM>())).ThrowsAsync(new InvalidOperationException("boom"));
            string shown = null;
            dialog.Setup(d => d.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), false))
                .Callback<string, string, bool>((text, _, _) => shown = text)
                .ReturnsAsync(true);
            var vm = new TransactionDialogVM(new TransactionDto { FromAmount = 1000, IsAmountNegative = true }, dialog.Object);

            await vm.OpenRecipesDialogCommand.ExecuteAsync();

            Assert.Contains("boom", shown);
            Assert.Empty(vm.Transaction.SubTransactions);
            Assert.Contains(this.Lines, line => line.Contains("[Transaction] opening the recipes wizard"));
            Assert.Contains(this.Lines, line => line.StartsWith("ERROR|") && line.Contains("[Transaction] the recipes wizard failed") && line.Contains("boom"));
        }

        [Fact]
        public async Task RecipesButton_PartsReturned_LoggedWithTheCounts()
        {
            var dialog = new Mock<IDialogWrapper>(MockBehavior.Strict);
            dialog.Setup(d => d.ShowWizardAsync(It.IsAny<RecipesVM>())).ReturnsAsync(new List<TransactionDto>
            {
                new TransactionDto { FromAmount = -549, IsAmountNegative = true },
            });
            var vm = new TransactionDialogVM(new TransactionDto { FromAmount = 549, IsAmountNegative = true }, dialog.Object);

            await vm.OpenRecipesDialogCommand.ExecuteAsync();

            Assert.Contains(this.Lines, line => line.Contains("the recipes wizard returned 1 parts"));
            Assert.Contains(this.Lines, line => line.Contains("[Transaction] 1 parts added, 1 in all"));
        }

        [Fact]
        public void GroupButton_LogsTheCounts()
        {
            var parent = new TransactionDto { FromAmount = 1000, IsAmountNegative = true };
            parent.SubTransactions.Add(new TransactionDto { CategoryId = 1, FromAmount = -100, IsAmountNegative = true });
            parent.SubTransactions.Add(new TransactionDto { CategoryId = 1, FromAmount = -200, IsAmountNegative = true });
            var vm = new TransactionDialogVM(parent, new Mock<IDialogWrapper>(MockBehavior.Strict).Object);

            vm.GroupSubTransactionsCommand.Execute();

            Assert.Contains(this.Lines, line => line.Contains("[Transaction] grouped by category: 2 parts -> 1"));
        }

        // ---- CrashDiagnostics

        [Fact]
        public void DescribeEnvironment_SystemRuntimeAndTheLibrariesLoaded()
        {
            var lines = CrashDiagnostics.DescribeEnvironment();

            Assert.StartsWith("Environment: ", lines[0]);
            Assert.Contains(".NET", lines[0]);
            Assert.StartsWith("Folder: ", lines[1]);
            Assert.All(lines.Skip(2), line => Assert.StartsWith("Library: ", line));
            Assert.Contains(lines, line => line.StartsWith("Library: UglyToad.PdfPig ")); // used by this test project through the app
        }

        [Fact]
        public void DescribeNativeModules_DoesNotThrow_AndOnlyNamesNativeLibraries()
        {
            var lines = CrashDiagnostics.DescribeNativeModules();

            Assert.All(lines, line => Assert.True(line.StartsWith("Native: ") || line.StartsWith("Native modules could not be listed"), line));
        }

        [Fact]
        public void Install_CalledTwice_IsFine()
        {
            CrashDiagnostics.Install();
            CrashDiagnostics.Install();
        }

        private sealed class FailingWizard : WizardBaseVM
        {
            public FailingWizard(int pages = 1)
            {
                _pages = Enumerable.Range(0, pages).Select(_ => (WizardPageBaseVM)new StubPage()).ToList().AsReadOnly();
                CurrentPage = Pages[0];
            }

            public bool Fail { get; set; } = true;

            public override void AfterCurrentPageUpdated(WizardPageBaseVM newValue)
            {
            }

            public override void BeforeCurrentPageUpdated(WizardPageBaseVM old, WizardPageBaseVM newValue)
            {
            }

            public override void CreatePages()
            {
            }

            public override object OnRequestClose(bool save) => Fail ? throw new InvalidOperationException("no result today") : "result";
        }

        private sealed class StubPage : WizardPageBaseVM
        {
            public override string Title => "stub";

            public override bool IsValid() => true;
        }
    }

    /// <summary>The tests that replace the global NLog configuration never run next to another test class.</summary>
    [CollectionDefinition("NLog configuration", DisableParallelization = true)]
    public sealed class NLogConfigurationCollection;
}
