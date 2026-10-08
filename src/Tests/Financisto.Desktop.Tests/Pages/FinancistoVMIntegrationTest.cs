namespace Financisto.Desktop.Tests.ViewModel
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Financisto.Adapter;
    using Financisto.BankHelpers;
    using Financisto.Common.Localization;
    using Financisto.DataAccess;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels;
    using Financisto.Desktop.ViewModels.Pages;
    using Financisto.Desktop.Views.Dialogs;
    using Moq;
    using Xunit;

    [Collection("Integration tests")]
    [CollectionDefinition("Integration tests", DisableParallelization = true)]
    public class FinancistoVMIntegrationTest
    {
        [Fact]
        public async Task OpenBackup_ParseBackup_ImportEntities()
        {
            var dialogMock = new Mock<IDialogWrapper>();
            dialogMock.Setup(x => x.ShowMessageBoxAsync(It.IsAny<string>(), "Success", false)).ReturnsAsync(true);
            var backupPath = Path.Combine(Environment.CurrentDirectory, "Assets", "min.backup");
            var vm = new MainWindowVM(dialogMock.Object, new FinancistoDatabaseFactory(), new EntityReader(), null, null, null);

            await vm.OpenBackup(backupPath);

            Assert.True(vm.CurrentPage is BlotterPageVM);
            Assert.Equal(backupPath, vm.OpenBackupPath);
        }

        [Fact]
        public async Task SaveBackup_OpenBackup_ShouldSaveSameBackup()
        {
            var dialogMock = new Mock<IDialogWrapper>();
            dialogMock.Setup(x => x.ShowMessageBoxAsync(It.IsAny<string>(), "Success", false)).ReturnsAsync(true);
            var backupPath = Path.Combine(Environment.CurrentDirectory, "Assets", "min.backup");
            var vm = new MainWindowVM(dialogMock.Object, new FinancistoDatabaseFactory(), new EntityReader(), new BackupWriter(), null, null);

            await vm.OpenBackup(backupPath);

            var newBackupPath = Path.Combine(Path.GetDirectoryName(backupPath), BackupWriter.GenerateFileName());

            await vm.SaveBackup(newBackupPath);

            Assert.True(File.Exists(newBackupPath));
            File.Delete(newBackupPath);
        }
    }
}
