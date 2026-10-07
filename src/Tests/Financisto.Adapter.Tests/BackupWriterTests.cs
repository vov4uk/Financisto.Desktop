namespace Financisto.Adapter.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Threading.Tasks;
    using Financisto.DataAccess.Data;
    using Financisto.Tests.Common;
    using Xunit;

    public class BackupWriterTests
    {
        [Theory]
        [AutoMoqData]
        public async Task GenerateBackup_ArchiveTransactions_FileExist(string fileName, BackupVersion version, List<Transaction> transactions)
        {
            Dictionary<string, List<string>> entityColumnsOrder = new Dictionary<string, List<string>>
            {
                { "transactions", PredefinedData.TransactionsColumnsOrder },
            };

            string path = fileName + ".backup";

            BackupWriter writer = new BackupWriter();

            await writer.GenerateBackupAsync(new List<Entity>(transactions), path, version, entityColumnsOrder);

            Assert.True(File.Exists(path));
            File.Delete(path);
        }

        [Fact]
        public async Task GenerateBackup_ParseBackup_CompareGeneratedFileWithRaw()
        {
            var backupPath = Path.Combine(Environment.CurrentDirectory, "Assets", "min.backup");
            var expectedTextPath = Path.Combine(Environment.CurrentDirectory, "Assets", "min");
            var actualPath = Path.Combine(Environment.CurrentDirectory, "Assets", "actual.backup");

            var reader = new EntityReader();
            var (entities, backupVersion, columnsOrder) = await reader.ParseBackupFileAsync(backupPath);

            BackupWriter writer = new BackupWriter();
            await writer.GenerateBackupAsync(entities, actualPath, backupVersion, columnsOrder);

            string actualText;
            using (var fileStream = new FileStream(actualPath, FileMode.Open))
            using (var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress))
            using (var streamReader = new StreamReader(gzipStream))
            {
                actualText = await streamReader.ReadToEndAsync();
            }

            // The writer emits Android's "\n" line endings; the reference file may be checked out with "\r\n".
            var expectedText = File.ReadAllText(expectedTextPath).Replace("\r\n", "\n");

            Assert.True(File.Exists(actualPath));
            Assert.Equal(expectedText, actualText);

            File.Delete(actualPath);
        }
    }
}
