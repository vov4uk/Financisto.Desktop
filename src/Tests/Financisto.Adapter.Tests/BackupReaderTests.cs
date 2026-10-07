namespace Financisto.Adapter.Tests
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Xunit;

    public class BackupReaderTests
    {
        [Fact]
        public async Task GetLines_ReadLinesFromArchive_ReadCorrectCount()
        {
            var backupPath = Path.Combine(Environment.CurrentDirectory, "Assets", "min.backup");

            using BackupReader backupReader = new BackupReader(backupPath);

            int count = 0;
            await foreach (var _ in backupReader.GetLinesAsync())
            {
                count++;
            }

            Assert.Equal(725, count);
            Assert.Equal(249, backupReader.BackupVersion.DatabaseVersion);
            Assert.Equal("tw.tib.financisto", backupReader.BackupVersion.Package);
            Assert.Equal("2026-09-11 d", backupReader.BackupVersion.Version);
            Assert.Equal(261, backupReader.BackupVersion.VersionCode);
        }

        [Fact]
        public async Task GetLines_ReadLinesFromArchive_KeepsUnicodeAndEscapedLineBreaks()
        {
            var backupPath = Path.Combine(Environment.CurrentDirectory, "Assets", "min.backup");

            using BackupReader backupReader = new BackupReader(backupPath);

            var lines = new System.Collections.Generic.List<string>();
            await foreach (var line in backupReader.GetLinesAsync())
            {
                lines.Add(line);
            }

            // Emoji outside the BMP survive the gzip/UTF-8 round trip.
            Assert.Contains("title:Food🥗", lines);
            Assert.Contains("note:💶", lines);

            // Aliases stay on one line: line breaks are the two characters '\' 'n', not real line breaks.
            Assert.Contains(@"aliases:ОККО\nOrlen", lines);
        }
    }
}
