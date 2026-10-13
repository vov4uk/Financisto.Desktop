namespace Financisto.Desktop.Tests.Wizards.Recipes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Wizards;
    using Financisto.Desktop.Wizards.Recipes;
    using Xunit;

    /// <summary>The recipes wizard remembers the category chosen for a note (a text file) and offers it the next time the note comes up.</summary>
    public sealed class RecipeCategoryMemoryTests : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "Financisto.Desktop.Tests", "recipe-categories", Guid.NewGuid().ToString("N"));
        private readonly string path;

        public RecipeCategoryMemoryTests()
        {
            this.path = Path.Combine(this.dir, "RecipeCategories.json");
            Directory.CreateDirectory(this.dir);
            DbManual.SetupTests(new List<CategoryModel>
            {
                new CategoryModel { Id = 0, Title = "<NO_CATEGORY>" },
                new CategoryModel { Id = 7, Title = "Dairy" },
                new CategoryModel { Id = 8, Title = "Mleko" },
                new CategoryModel { Id = 9, Title = "Bread" },
            });
        }

        public void Dispose()
        {
            DbManual.ResetAllDatabaseManuals();
            Directory.Delete(this.dir, true);
        }

        // ---- the file

        [Fact]
        public void Load_NoFile_NothingRemembered()
        {
            var memory = new RecipeCategoryMemory(this.path);

            memory.Load();

            Assert.Empty(memory.Categories);
        }

        [Theory]
        [InlineData("this is not json")]
        [InlineData("[1, 2, 3]")]
        [InlineData("{\"note\": 5}")]
        [InlineData("")]
        public void Load_DamagedFile_NothingRemembered(string content)
        {
            File.WriteAllText(this.path, content);
            var memory = new RecipeCategoryMemory(this.path);

            memory.Load();

            Assert.Empty(memory.Categories);
        }

        [Fact]
        public void Load_ReadsTheNotesAndTitlesOfTheFile()
        {
            File.WriteAllText(this.path, "{ \"Mleko UHT\": \"Dairy\", \"  chleb \": \" Bread\", \"\": \"x\", \"empty\": \" \" }");
            var memory = new RecipeCategoryMemory(this.path);

            memory.Load();

            Assert.Equal(2, memory.Categories.Count);
            Assert.Equal("Dairy", memory.Categories["Mleko UHT"]);
            Assert.Equal("Bread", memory.Categories["chleb"]);
        }

        [Fact]
        public void SaveAndLoad_RoundTrip_NotesAreCaseInsensitiveAndReadableInTheFile()
        {
            var memory = new RecipeCategoryMemory(this.path);
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel { Id = 3, Title = "Owoce i warzywa" } });
            memory.Remember(new[] { new FinancistoTransactionDto { Note = "Śliwki węgierki", CategoryId = 3 } });

            memory.Save();

            var text = File.ReadAllText(this.path);
            Assert.Contains("Śliwki węgierki", text); // not Ś...
            Assert.Contains("Owoce i warzywa", text);
            var loaded = new RecipeCategoryMemory(this.path);
            loaded.Load();
            Assert.True(loaded.TryGetCategoryId("ŚLIWKI WĘGIERKI", out var id));
            Assert.Equal(3, id);
        }

        [Fact]
        public void Save_FolderDoesNotExist_Created()
        {
            var nested = Path.Combine(this.dir, "a", "b", "RecipeCategories.json");
            var memory = new RecipeCategoryMemory(nested);
            memory.Remember(new[] { new FinancistoTransactionDto { Note = "mleko", CategoryId = 7 } });

            memory.Save();

            Assert.True(File.Exists(nested));
            Assert.False(File.Exists(nested + ".tmp"));
        }

        [Fact]
        public void Save_FileCannotBeWritten_DoesNotThrow()
        {
            Directory.CreateDirectory(this.path); // a folder where the file should be
            var memory = new RecipeCategoryMemory(this.path);
            memory.Remember(new[] { new FinancistoTransactionDto { Note = "mleko", CategoryId = 7 } });

            var exception = Record.Exception(memory.Save);

            Assert.Null(exception);
        }

        [Fact]
        public void DefaultPath_NextToTheSettings()
        {
            Assert.Equal("RecipeCategories.json", Path.GetFileName(RecipeCategoryMemory.DefaultPath));
            Assert.Equal(Path.GetDirectoryName(Path.GetFullPath(StartOptions.Current.SettingsPath)), Path.GetDirectoryName(RecipeCategoryMemory.DefaultPath));
        }

        // ---- what is remembered

        [Fact]
        public void Remember_RowsWithNoteAndCategory_NoteToCategoryTitle()
        {
            var memory = new RecipeCategoryMemory(this.path);

            memory.Remember(new[]
            {
                new FinancistoTransactionDto { Note = "Mleko UHT", CategoryId = 7 },
                new FinancistoTransactionDto { Note = "  Chleb  ", CategoryId = 9 },
            });

            Assert.Equal("Dairy", memory.Categories["Mleko UHT"]);
            Assert.Equal("Bread", memory.Categories["Chleb"]);
        }

        [Theory]
        [InlineData(null, 7)]
        [InlineData("", 7)]
        [InlineData("   ", 7)]
        public void Remember_RowWithoutNote_Ignored(string note, int categoryId)
        {
            var memory = new RecipeCategoryMemory(this.path);

            memory.Remember(new[] { new FinancistoTransactionDto { Note = note, CategoryId = categoryId } });

            Assert.Empty(memory.Categories);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(99)] // no such category
        public void Remember_RowWithoutARealCategory_Ignored(int categoryId)
        {
            var memory = new RecipeCategoryMemory(this.path);

            memory.Remember(new[] { new FinancistoTransactionDto { Note = "mleko", CategoryId = categoryId } });

            Assert.Empty(memory.Categories);
        }

        [Fact]
        public void Remember_RowWithoutCategory_KeepsWhatWasKnown()
        {
            var memory = new RecipeCategoryMemory(this.path);
            memory.Remember(new[] { new FinancistoTransactionDto { Note = "mleko", CategoryId = 7 } });

            memory.Remember(new[] { new FinancistoTransactionDto { Note = "mleko", CategoryId = 0 } });

            Assert.Equal("Dairy", memory.Categories["mleko"]);
        }

        [Fact]
        public void Remember_NoteSeenAgainWithAnotherCategory_LatestChoiceWins()
        {
            var memory = new RecipeCategoryMemory(this.path);
            memory.Remember(new[] { new FinancistoTransactionDto { Note = "Mleko", CategoryId = 7 } });

            memory.Remember(new[] { new FinancistoTransactionDto { Note = "mleko", CategoryId = 9 } });

            Assert.Equal("Bread", Assert.Single(memory.Categories).Value);
        }

        // ---- finding it again

        [Fact]
        public void TryGetCategoryId_RememberedNote_IdOfTheCategoryWithThatTitle()
        {
            File.WriteAllText(this.path, "{ \"Mleko UHT\": \"dairy\" }");
            var memory = new RecipeCategoryMemory(this.path);
            memory.Load();

            Assert.True(memory.TryGetCategoryId("mleko uht ", out var id));
            Assert.Equal(7, id);
        }

        [Theory]
        [InlineData("Never seen")]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void TryGetCategoryId_UnknownOrBlankNote_False(string note)
        {
            File.WriteAllText(this.path, "{ \"Mleko UHT\": \"Dairy\" }");
            var memory = new RecipeCategoryMemory(this.path);
            memory.Load();

            Assert.False(memory.TryGetCategoryId(note, out var id));
            Assert.Equal(0, id);
        }

        [Fact]
        public void TryGetCategoryId_CategoryRenamedOrDeleted_False()
        {
            File.WriteAllText(this.path, "{ \"Mleko UHT\": \"Dairy\" }");
            var memory = new RecipeCategoryMemory(this.path);
            memory.Load();
            DbManual.SetupTests(new List<CategoryModel> { new CategoryModel { Id = 7, Title = "Milk products" } });

            Assert.False(memory.TryGetCategoryId("Mleko UHT", out _));
        }

        [Fact]
        public void TryGetCategoryId_TitleOfTheNoCategoryRow_False()
        {
            File.WriteAllText(this.path, "{ \"x\": \"<NO_CATEGORY>\" }");
            var memory = new RecipeCategoryMemory(this.path);
            memory.Load();

            Assert.False(memory.TryGetCategoryId("x", out _));
        }

        [Fact]
        public void TryGetCategoryId_SameTitleTwice_FirstOne()
        {
            File.WriteAllText(this.path, "{ \"x\": \"Other\" }");
            var memory = new RecipeCategoryMemory(this.path);
            memory.Load();
            DbManual.SetupTests(new List<CategoryModel>
            {
                new CategoryModel { Id = 20, Title = "Other" },
                new CategoryModel { Id = 21, Title = "Other" },
            });

            Assert.True(memory.TryGetCategoryId("x", out var id));
            Assert.Equal(20, id);
        }

        // ---- TryParseCategory of the first page

        [Fact]
        public void Page1_RememberedNote_CategoryOfTheMemoryBeforeTheTitleWords()
        {
            File.WriteAllText(this.path, "{ \"Mleko UHT\": \"Bread\" }");
            var memory = new RecipeCategoryMemory(this.path);
            memory.Load();
            var page1 = new Page1VM(-5.49, memory) { Text = "Mleko UHT 1l B 1.000 x 5,49 5,49" };

            page1.CalculateCurrentAmount();

            var row = Assert.Single(page1.Amounts);
            Assert.Equal("Mleko UHT", row.Note);
            Assert.Equal(9, row.CategoryId); // not 8: the title "Mleko" is only the guess for notes never seen
        }

        [Fact]
        public void Page1_NoteNotRemembered_CategoryFromTheTitleWords()
        {
            var memory = new RecipeCategoryMemory(this.path);
            memory.Load();
            var page1 = new Page1VM(-5.49, memory) { Text = "Mleko UHT 1l B 1.000 x 5,49 5,49" };

            page1.CalculateCurrentAmount();

            Assert.Equal(8, Assert.Single(page1.Amounts).CategoryId);
        }

        [Fact]
        public void Page1_NoMemory_CategoryFromTheTitleWords()
        {
            var page1 = new Page1VM(-5.49) { Text = "Mleko UHT 1l B 1.000 x 5,49 5,49" };

            page1.CalculateCurrentAmount();

            Assert.Equal(8, Assert.Single(page1.Amounts).CategoryId);
        }

        [Fact]
        public void Page1_MarkedAmountLines_UseTheMemoryToo()
        {
            File.WriteAllText(this.path, "{ \"Beer Baltyka\": \"Bread\" }");
            var memory = new RecipeCategoryMemory(this.path);
            memory.Load();
            var page1 = new Page1VM(-50.0, memory) { Text = "Beer Baltyka 0 50,0 A" };

            page1.CalculateCurrentAmount();

            Assert.Equal(9, Assert.Single(page1.Amounts).CategoryId);
        }

        // ---- Clear notes

        [Fact]
        public void ClearNotes_RowsWithNoteAndCategory_RememberedBeforeTheNotesGo()
        {
            var memory = new RecipeCategoryMemory(this.path);
            var page2 = new Page2VM(-10.0, memory);
            var milk = new FinancistoTransactionDto { Note = "Mleko UHT", CategoryId = 7, Order = 1 };
            var bread = new FinancistoTransactionDto { Note = "Chleb", CategoryId = 9, Order = 2 };
            var noCategory = new FinancistoTransactionDto { Note = "Kaucja", CategoryId = 0, Order = 3 };
            var noNote = new FinancistoTransactionDto { Note = null, CategoryId = 9, Order = 4 };
            page2.SetTransactions(new List<FinancistoTransactionDto> { milk, bread, noCategory, noNote });

            page2.ClearAllNotesCommand.Execute();

            Assert.All(page2.FinancistoTransactions, row => Assert.Null(row.Note));
            Assert.Equal(new[] { "Chleb", "Mleko UHT" }, memory.Categories.Keys.OrderBy(key => key));
            Assert.Equal("Dairy", memory.Categories["Mleko UHT"]);
            Assert.False(File.Exists(this.path)); // the file is written when the wizard is saved
        }

        [Fact]
        public void ClearNotes_NoMemory_JustClears()
        {
            var page2 = new Page2VM(-10.0);
            page2.SetTransactions(new List<FinancistoTransactionDto> { new FinancistoTransactionDto { Note = "Mleko", CategoryId = 7 } });

            page2.ClearAllNotesCommand.Execute();

            Assert.Null(page2.FinancistoTransactions[0].Note);
        }

        // ---- the wizard

        [Fact]
        public void Wizard_Starts_PreviousChoicesAreAppliedToTheParsedRows()
        {
            File.WriteAllText(this.path, "{ \"Mleko UHT\": \"Dairy\", \"Chleb\": \"Bread\" }");
            var vm = this.CreateWizard(-10.98);
            ((Page1VM)vm.CurrentPage).Text = "Mleko UHT B 1.000 x 5,49 5,49\r\nChleb B 1.000 x 5,49 5,49\r\nNowy produkt B 1.000 x 0,01 0,01";

            vm.MoveNextCommand.Execute();

            var rows = ((Page2VM)vm.CurrentPage).FinancistoTransactions;
            Assert.Equal(new[] { 7, 9, 0 }, rows.Select(row => row.CategoryId));
        }

        [Fact]
        public void Wizard_Saved_ChoicesWrittenToTheFile()
        {
            var vm = this.CreateWizard(-5.49);
            ((Page1VM)vm.CurrentPage).Text = "Mleko UHT B 1.000 x 5,49 5,49";
            vm.MoveNextCommand.Execute();
            var row = ((Page2VM)vm.CurrentPage).FinancistoTransactions.Single();
            row.CategoryId = 7; // chosen by the user

            vm.MoveNextCommand.Execute(); // finish

            var written = new RecipeCategoryMemory(this.path);
            written.Load();
            Assert.Equal("Dairy", Assert.Single(written.Categories).Value);
            Assert.True(written.TryGetCategoryId("Mleko UHT", out var id));
            Assert.Equal(7, id);
        }

        [Fact]
        public void Wizard_NotesClearedBeforeSaving_ChoicesStillWritten()
        {
            var vm = this.CreateWizard(-10.98);
            ((Page1VM)vm.CurrentPage).Text = "Mleko UHT B 1.000 x 5,49 5,49\r\nChleb B 1.000 x 5,49 5,49";
            vm.MoveNextCommand.Execute();
            var page2 = (Page2VM)vm.CurrentPage;
            page2.FinancistoTransactions[0].CategoryId = 7;
            page2.FinancistoTransactions[1].CategoryId = 9;
            List<TransactionDto> output = null;
            vm.RequestClose += (parts, save) => output = save ? parts as List<TransactionDto> : null;

            page2.ClearAllNotesCommand.Execute();
            vm.MoveNextCommand.Execute(); // finish

            Assert.All(output, part => Assert.True(string.IsNullOrEmpty(part.Note))); // the notes are gone from the transactions...
            var written = new RecipeCategoryMemory(this.path);
            written.Load();
            Assert.Equal(new[] { "Chleb", "Mleko UHT" }, written.Categories.Keys.OrderBy(key => key)); // ...and remembered
        }

        [Fact]
        public void Wizard_SavedWithRowsWithoutNote_TheyAreNotWritten()
        {
            var vm = this.CreateWizard(-5.49);
            ((Page1VM)vm.CurrentPage).Text = "Mleko UHT B 1.000 x 5,49 5,49";
            vm.MoveNextCommand.Execute();
            var page2 = (Page2VM)vm.CurrentPage;
            page2.FinancistoTransactions[0].CategoryId = 7;
            page2.AddRowCommand.Execute();
            page2.FinancistoTransactions[1].CategoryId = 9; // a row added by hand, no note

            vm.MoveNextCommand.Execute();

            var written = new RecipeCategoryMemory(this.path);
            written.Load();
            Assert.Equal(new[] { "Mleko UHT" }, written.Categories.Keys);
        }

        [Fact]
        public void Wizard_Cancelled_NothingWritten()
        {
            var vm = this.CreateWizard(-5.49);
            ((Page1VM)vm.CurrentPage).Text = "Mleko UHT B 1.000 x 5,49 5,49";
            vm.MoveNextCommand.Execute();
            ((Page2VM)vm.CurrentPage).FinancistoTransactions.Single().CategoryId = 7;

            vm.CancelCommand.Execute();

            Assert.False(File.Exists(this.path));
        }

        [Fact]
        public void Wizard_Saved_EarlierChoicesOfOtherReceiptsKept()
        {
            File.WriteAllText(this.path, "{ \"Chleb\": \"Bread\" }");
            var vm = this.CreateWizard(-5.49);
            ((Page1VM)vm.CurrentPage).Text = "Mleko UHT B 1.000 x 5,49 5,49";
            vm.MoveNextCommand.Execute();
            ((Page2VM)vm.CurrentPage).FinancistoTransactions.Single().CategoryId = 7;

            vm.MoveNextCommand.Execute();

            var written = new RecipeCategoryMemory(this.path);
            written.Load();
            Assert.Equal(new[] { "Chleb", "Mleko UHT" }, written.Categories.Keys.OrderBy(key => key));
        }

        private RecipesVM CreateWizard(double total)
        {
            DbManual.SetupTests(new List<AccountFilterModel>());
            DbManual.SetupTests(new List<CurrencyModel>());
            return new RecipesVM(total, new RecipeCategoryMemory(this.path));
        }
    }
}
