namespace Financisto.Desktop.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Financisto.Adapter;
    using Financisto.Common.Entities;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.ViewModels;
    using Moq;
    using Xunit;

    /// <summary>
    /// Runs Assets/min.backup through the real reader, the in-memory database and the writer. The file is a backup exported
    /// by the app itself (with splits, split transfers, three currencies, tags, aliases), so what Android/Financisto computed
    /// for it is the reference for the balance logic.
    /// </summary>
    [Collection("Integration tests")]
    public class MinBackupIntegrationTests
    {
        private static string BackupPath => Path.Combine(Environment.CurrentDirectory, "Assets", "min.backup");

        [Fact]
        public async Task Import_MinBackup_AccountTotalsMatchBackup()
        {
            var (entities, db) = await ImportMinBackup();
            using (db)
            {
                var expected = entities.OfType<Account>().ToDictionary(x => x.Id, x => x.TotalAmount);

                var actual = (await GetAll<Account>(db)).ToDictionary(x => x.Id, x => x.TotalAmount);

                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public async Task RebuildAccountBalance_EveryAccount_ReproducesBackupTotals()
        {
            var (entities, db) = await ImportMinBackup();
            using (db)
            {
                var expected = entities.OfType<Account>().ToDictionary(x => x.Id, x => x.TotalAmount);

                foreach (var accountId in expected.Keys)
                {
                    await db.RebuildAccountBalanceAsync(accountId);
                }

                var actual = (await GetAll<Account>(db)).ToDictionary(x => x.Id, x => x.TotalAmount);
                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public async Task RebuildAccountBalance_LatestRunningBalance_HoldsAccountTotal()
        {
            var (entities, db) = await ImportMinBackup();
            using (db)
            {
                var expected = entities.OfType<Account>().ToDictionary(x => x.Id, x => x.TotalAmount);
                foreach (var accountId in expected.Keys)
                {
                    await db.RebuildAccountBalanceAsync(accountId);
                }

                var lastBalances = await db.GetLastRunningBalancesAsync();

                Assert.Equal(expected, lastBalances);
            }
        }

        [Fact]
        public async Task Import_MinBackup_KeepsAllEntities()
        {
            var (entities, db) = await ImportMinBackup();
            using (db)
            {
                Assert.Equal(entities.OfType<Transaction>().Count(), (await GetAll<Transaction>(db)).Count);
                Assert.Equal(entities.OfType<Currency>().Count(), (await GetAll<Currency>(db)).Count);
                Assert.Equal(entities.OfType<CurrencyExchangeRate>().Count(), (await GetAll<CurrencyExchangeRate>(db)).Count);
                Assert.Equal(entities.OfType<Tag>().Count(), (await GetAll<Tag>(db)).Count(x => x.Id > 0));
                Assert.Equal(entities.OfType<Payee>().Count(), (await GetAll<Payee>(db)).Count);
                Assert.Equal(entities.OfType<Location>().Count(), (await GetAll<Location>(db)).Count(x => x.Id > 0));
                Assert.Equal(entities.OfType<Project>().Count(), (await GetAll<Project>(db)).Count(x => x.Id > 0));
            }
        }

        [Fact]
        public async Task Import_MinBackup_DbManualListsTagsAndCurrencies()
        {
            var (_, db) = await ImportMinBackup();
            using (db)
            {
                DbManual.ResetAllDatabaseManuals();
                await DbManual.SetupAsync(db);

                try
                {
                    Assert.Equal(new[] { "Tag1", "Tag2" }, DbManual.Tag.Where(x => x.Id > 0).Select(x => x.Title).OrderBy(x => x));
                    Assert.Equal(new[] { "EUR", "UAH", "USD" }, DbManual.Currencies.Where(x => x.Id > 0).Select(x => x.Name).OrderBy(x => x));
                    Assert.Equal(5, DbManual.Account.Count(x => x.Id.HasValue));
                }
                finally
                {
                    DbManual.ResetAllDatabaseManuals();
                }
            }
        }

        [Fact]
        public async Task SaveBackup_OpenMinBackup_WritesSameTextAsSource()
        {
            var savedPath = Path.Combine(Path.GetTempPath(), "Financisto.Desktop.Tests", BackupWriter.GenerateFileName());
            Directory.CreateDirectory(Path.GetDirectoryName(savedPath));
            var vm = new MainWindowVM(
                new Mock<Financisto.Desktop.Helpers.IDialogWrapper>().Object,
                new FinancistoDatabaseFactory(),
                new EntityReader(),
                new BackupWriter(),
                null,
                null,
                null);
            try
            {
                await vm.OpenBackup(BackupPath);
                await vm.SaveBackup(savedPath);

                Assert.Equal(Normalize(ReadArchive(BackupPath)), Normalize(ReadArchive(savedPath)));
            }
            finally
            {
                DbManual.ResetAllDatabaseManuals();
                File.Delete(savedPath);
            }
        }

        private static async Task<(List<Entity> Entities, IFinancistoDatabase Db)> ImportMinBackup()
        {
            var (entities, _, _) = await new EntityReader().ParseBackupFileAsync(BackupPath);
            var list = entities.ToList();

            var db = new FinancistoDatabaseFactory().CreateDatabase();
            await db.ImportEntitiesAsync(list);
            return (list, db);
        }

        private static async Task<List<T>> GetAll<T>(IFinancistoDatabase db)
            where T : Entity
        {
            using var uow = db.CreateUnitOfWork();
            return await uow.GetRepository<T>().GetAllAsync();
        }

        /// <summary>
        /// Evens out the two known differences between a backup and the same data coming back from the database:
        /// location accuracy/latitude/longitude are strings in REAL columns, so "0" returns as "0.0", and exchange rates
        /// come back in primary key order instead of file order.
        /// </summary>
        private static string Normalize(string backupText)
        {
            var text = Regex.Replace(backupText, @"^(accuracy|latitude|longitude):(-?\d+)\.0$", "$1:$2", RegexOptions.Multiline);

            return Regex.Replace(
                text,
                @"(?:\$ENTITY:currency_exchange_rate\n(?:.*\n)*?\$\$\n)+",
                run => string.Concat(Regex.Matches(run.Value, @"\$ENTITY:currency_exchange_rate\n(?:.*\n)*?\$\$\n")
                    .Select(x => x.Value)
                    .OrderBy(x => x, StringComparer.Ordinal)));
        }

        private static string ReadArchive(string path)
        {
            using var file = File.OpenRead(path);
            using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            return reader.ReadToEnd();
        }
    }
}
