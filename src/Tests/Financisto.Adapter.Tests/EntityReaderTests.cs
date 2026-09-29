namespace Financisto.Adapter.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.DataAccess.Data;
    using Financisto.Tests.Common;
    using Xunit;

    public class EntityReaderTests
    {
        [Fact]
        public async Task ParseBackupFile_ReadEntitiesFromArchive_ReadCorrectCount()
        {
            var (entities, backupVersion, columnsOrder) = await ParseMinBackup();

            Assert.Equal(249, backupVersion.DatabaseVersion);
            Assert.Equal("tw.tib.financisto", backupVersion.Package);
            Assert.Equal("2026-09-11 d", backupVersion.Version);
            Assert.Equal(261, backupVersion.VersionCode);

            Assert.Equal(5, entities.OfType<Account>().Count());
            Assert.Equal(7, entities.OfType<Category>().Count());
            Assert.Equal(16, entities.OfType<Transaction>().Count());
            Assert.Equal(3, entities.OfType<Currency>().Count());
            Assert.Equal(2, entities.OfType<CurrencyExchangeRate>().Count());
            Assert.Equal(2, entities.OfType<Location>().Count());
            Assert.Equal(2, entities.OfType<Payee>().Count());
            Assert.Equal(2, entities.OfType<Tag>().Count());
            Assert.Single(entities.OfType<Project>());
            Assert.Single(entities.OfType<Budget>());
            Assert.Single(entities.OfType<AttributeDefinition>());
            Assert.Single(entities.OfType<CategoryAttribute>());
            Assert.Empty(entities.OfType<SmsTemplate>());
            // Columns are kept in the order the file lists them (Financisto appended parent_account_id and tags).
            Assert.Equal(
                PredefinedData.TransactionsColumnsOrder.Concat(new[] { "parent_account_id", "tags" }),
                columnsOrder["transactions"]);
        }

        [Fact]
        public async Task ParseBackupFile_Transaction_ReadAllColumns()
        {
            var (entities, _, _) = await ParseMinBackup();

            var transaction = entities.OfType<Transaction>().Single(x => x.Id == 3);

            Assert.Equal(3, transaction.FromAccountId);
            Assert.Equal(2, transaction.CategoryId);
            Assert.Equal(-55000, transaction.FromAmount);
            Assert.Equal(1515338499910, transaction.DateTime);
            Assert.Equal("PN", transaction.Status);
            Assert.Equal(0, transaction.ParentId);
            Assert.Null(transaction.Note);
            Assert.Null(transaction.Tags);
        }

        [Fact]
        public async Task ParseBackupFile_TransactionWithTagPayeeProjectAndLocation_ReadReferences()
        {
            var (entities, _, _) = await ParseMinBackup();

            var transaction = entities.OfType<Transaction>().Single(x => x.Id == 45040);

            Assert.Equal("Tag2", transaction.Tags);
            Assert.Equal(531, transaction.PayeeId);
            Assert.Equal(58, transaction.ProjectId);
            Assert.Equal(103, transaction.LocationId);
            Assert.Equal(1, transaction.CategoryId);
            Assert.Equal(-10000, transaction.FromAmount);
        }

        [Fact]
        public async Task ParseBackupFile_SplitTransaction_PartsPointToParent()
        {
            var (entities, _, _) = await ParseMinBackup();
            var transactions = entities.OfType<Transaction>().ToList();

            var parent = transactions.Single(x => x.Id == 5);
            var parts = transactions.Where(x => x.ParentId == parent.Id).OrderBy(x => x.Id).ToList();

            Assert.Equal(-1, parent.CategoryId);
            Assert.Equal(2, parts.Count);
            Assert.Equal(parent.FromAmount, parts.Sum(x => x.FromAmount));
            Assert.All(parts, x => Assert.Equal(parent.FromAccountId, x.ParentAccountId));
            Assert.Equal(new[] { 2, 4 }, parts.Select(x => x.CategoryId));
        }

        [Fact]
        public async Task ParseBackupFile_SplitWithTransferPart_ReadBothAccounts()
        {
            var (entities, _, _) = await ParseMinBackup();
            var transactions = entities.OfType<Transaction>().ToList();

            var parent = transactions.Single(x => x.Id == 45037);
            var transferPart = transactions.Single(x => x.Id == 45039);

            Assert.Equal(60, parent.FromAccountId);
            Assert.Equal(parent.Id, transferPart.ParentId);
            Assert.Equal(60, transferPart.FromAccountId);
            Assert.Equal(3, transferPart.ToAccountId);
            Assert.Equal(60, transferPart.ParentAccountId);
            Assert.Equal(-2000, transferPart.FromAmount);
            Assert.Equal(100000, transferPart.ToAmount);
        }

        [Fact]
        public async Task ParseBackupFile_TransferBetweenCurrencies_ReadBothAmounts()
        {
            var (entities, _, _) = await ParseMinBackup();

            var transfer = entities.OfType<Transaction>().Single(x => x.Id == 45041);

            Assert.Equal(59, transfer.FromAccountId);
            Assert.Equal(60, transfer.ToAccountId);
            Assert.Equal(-10000, transfer.FromAmount);
            Assert.Equal(11000, transfer.ToAmount);
        }

        [Fact]
        public async Task ParseBackupFile_Accounts_ReadTypesCurrenciesAndSortOrder()
        {
            var (entities, _, _) = await ParseMinBackup();
            var accounts = entities.OfType<Account>().ToDictionary(x => x.Id);

            Assert.Equal(new[] { 3, 2, 1, 59, 60 }, entities.OfType<Account>().Select(x => x.Id));

            Assert.Equal("Credit card UAH", accounts[3].Title);
            Assert.Equal("CREDIT_CARD", accounts[3].Type);
            Assert.Equal("MASTERCARD", accounts[3].CardIssuer);
            Assert.Equal(-10000000, accounts[3].LimitAmount);
            Assert.Equal(17, accounts[3].ClosingDay);
            Assert.Equal(20, accounts[3].PaymentDay);

            Assert.Equal("ELECTRONIC", accounts[59].Type);
            Assert.Equal("PAYPAL", accounts[59].CardIssuer);
            Assert.Equal(8, accounts[59].CurrencyId);
            Assert.Equal("💵", accounts[59].Note);

            Assert.Equal(9, accounts[60].CurrencyId);
            Assert.Equal("GOOGLE_WALLET", accounts[60].CardIssuer);

            Assert.Equal(new[] { 1, 2, 5, 6, 7 }, entities.OfType<Account>().Select(x => x.SortOrder));
            Assert.All(accounts.Values, x => Assert.True(x.IsIncludeIntoReports));
            Assert.All(accounts.Values, x => Assert.Empty(x.Icon));
            Assert.All(accounts.Values, x => Assert.Empty(x.AccentColor));
        }

        [Fact]
        public async Task ParseBackupFile_Currencies_ReadFormatAndDefault()
        {
            var (entities, _, _) = await ParseMinBackup();
            var currencies = entities.OfType<Currency>().ToDictionary(x => x.Name);

            Assert.Equal(new[] { "UAH", "USD", "EUR" }, entities.OfType<Currency>().Select(x => x.Name));
            Assert.Equal(new[] { 7, 8, 9 }, entities.OfType<Currency>().Select(x => x.Id));
            Assert.Equal("₴", currencies["UAH"].Symbol);
            Assert.Equal("€", currencies["EUR"].Symbol);
            Assert.True(currencies["UAH"].IsDefault);
            Assert.False(currencies["USD"].IsDefault);
            Assert.False(currencies["EUR"].IsDefault);
            Assert.All(currencies.Values, x => Assert.Equal(2, x.Decimals));
        }

        [Fact]
        public async Task ParseBackupFile_ExchangeRates_ReadRateWithFullPrecision()
        {
            var (entities, _, _) = await ParseMinBackup();
            var rates = entities.OfType<CurrencyExchangeRate>().ToList();

            var uahToEur = rates.Single(x => x.FromCurrencyId == 7 && x.ToCurrencyId == 9);
            var uahToUsd = rates.Single(x => x.FromCurrencyId == 7 && x.ToCurrencyId == 8);

            Assert.Equal(0.0195945, uahToEur.Rate);
            Assert.Equal(0.0222923, uahToUsd.Rate);
            Assert.Equal(1790200800000, uahToEur.Date);
        }

        [Fact]
        public async Task ParseBackupFile_Tags_ReadTitles()
        {
            var (entities, _, _) = await ParseMinBackup();

            var tags = entities.OfType<Tag>().ToList();

            Assert.Equal(new[] { "Tag1", "Tag2" }, tags.Select(x => x.Title));
            Assert.Equal(new[] { 7, 8 }, tags.Select(x => x.Id));
            Assert.All(tags, x => Assert.True(x.IsActive));
        }

        [Fact]
        public async Task ParseBackupFile_LocationsAndPayeesWithAliases_AliasesKeptEscaped()
        {
            var (entities, _, _) = await ParseMinBackup();

            var supermarket = entities.OfType<Location>().Single(x => x.Id == 103);
            var gasStation = entities.OfType<Location>().Single(x => x.Id == 104);
            var mom = entities.OfType<Payee>().Single(x => x.Id == 531);

            // Aliases are stored the way Android exports them: one line, '\n' as two characters.
            Assert.Equal(@"Auchan \nATB\nKaufland\nСільпо", supermarket.Aliases);
            Assert.Equal(@"ОККО\nOrlen", gasStation.Aliases);
            Assert.Equal(@"Мама\nMama\nMatka", mom.Aliases);
            Assert.Equal("Gas station ⛽", gasStation.Title);
            Assert.Equal(2, gasStation.Count);
            Assert.Equal(1, mom.LastCategoryId);
        }

        [Fact]
        public async Task ParseBackupFile_CategoriesWithEmoji_ReadTitlesAndTree()
        {
            var (entities, _, _) = await ParseMinBackup();
            var categories = entities.OfType<Category>().ToDictionary(x => x.Id);

            Assert.Equal("Food🥗", categories[1].Title);
            Assert.Equal("Public transport🚋", categories[162].Title);
            Assert.Equal("Salary📆", categories[6].Title);

            // Nested set: a child sits inside its parent's left/right range.
            Assert.True(categories[2].Left > categories[1].Left && categories[2].Right < categories[1].Right);
            Assert.True(categories[162].Left > categories[3].Left && categories[162].Right < categories[3].Right);

            Assert.Equal(1, categories[5].Type);
            Assert.Equal(1, categories[6].Type);
            Assert.Equal(103, categories[1].LastLocationId);
            Assert.Equal(58, categories[1].LastProjectId);
        }

        [Fact]
        public async Task ParseBackupFile_BudgetProjectAndAttributes_ReadValues()
        {
            var (entities, _, _) = await ParseMinBackup();

            var budget = entities.OfType<Budget>().Single();
            var project = entities.OfType<Project>().Single();
            var attribute = entities.OfType<AttributeDefinition>().Single();
            var categoryAttribute = entities.OfType<CategoryAttribute>().Single();

            Assert.Equal("Budget", budget.Title);
            Assert.Equal(-2000000, budget.Amount);
            Assert.StartsWith("NO_RECUR,startDate=1790200800000", budget.Recur);
            Assert.Equal("Happy birthday🎈", project.Title);
            Assert.Equal("Number", attribute.Title);
            Assert.Equal(162, categoryAttribute.CategoryId);
            Assert.Equal(23, categoryAttribute.AttributeId);
        }

        private static async Task<(IEnumerable<Entity> Entities, BackupVersion BackupVersion, Dictionary<string, List<string>> ColumnsOrder)> ParseMinBackup()
        {
            var backupPath = Path.Combine(Environment.CurrentDirectory, "Assets", "min.backup");

            var reader = new EntityReader();
            var (entities, backupVersion, columnsOrder) = await reader.ParseBackupFileAsync(backupPath);
            return (entities, backupVersion, columnsOrder);
        }
    }
}
