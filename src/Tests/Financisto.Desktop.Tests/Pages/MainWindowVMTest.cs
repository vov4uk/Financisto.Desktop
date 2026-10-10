namespace Financisto.Desktop.Tests.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Financisto.BankHelpers;
    using Financisto.Adapter;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Financisto.DataAccess.View;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Services;
    using Financisto.Desktop.ViewModels;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Financisto.Desktop.ViewModels.Pages;
    using Financisto.Desktop.Views;
    using Financisto.Desktop.Views.Dialogs;
    using Financisto.Desktop.Wizards;
    using Financisto.Desktop.Wizards.MonoWizard.ViewModel;
    using Financisto.Tests.Common;
    using Moq;
    using Xunit;
    using Path = System.IO.Path;

    public class MainWindowVMTest
    {
        private readonly Mock<IBaseRepository<Account>> accountsRepo;
        private readonly Mock<IBackupWriter> backupWriterMock;
        private readonly Mock<IBaseRepository<Category>> categoriesRepo;
        private readonly Mock<IBankHelper> csvMock;
        private readonly Mock<IFinancistoDatabaseFactory> dbFactoryMock;
        private readonly Mock<IFinancistoDatabase> dbMock;
        private readonly Mock<IDialogWrapper> dialogMock;
        private readonly Mock<IEntityReader> entityReaderMock;
        private readonly Mock<IBaseRepository<CurrencyExchangeRate>> exchangeRatesRepo;
        private readonly Mock<IBaseRepository<BlotterTransactions>> transactionsRepo;
        private readonly Mock<IUnitOfWork> uowMock;
        private readonly Mock<IToastNotifierWrapper> toastNotifierMock;
        private Mock<IBaseRepository<AttributeDefinition>> adMock;
        private Mock<IBaseRepository<Budget>> budgetMock;
        private Mock<IBaseRepository<CategoryAttribute>> caMock;
        private Mock<IBaseRepository<CCardClosingDate>> cccdMock;
        private Mock<IBaseRepository<Currency>> currMock;
        private Mock<IBaseRepository<Location>> locMock;
        private Mock<IBaseRepository<Payee>> payeeMock;
        private Mock<IBaseRepository<Project>> projMock;
        private Mock<IBaseRepository<SmsTemplate>> smsMock;
        private Mock<IBaseRepository<Tag>> tagMock;
        private Mock<IBaseRepository<TransactionAttribute>> trAMock;
        private Mock<IBaseRepository<Transaction>> trMock;

        public MainWindowVMTest()
        {
            this.csvMock = new (MockBehavior.Strict);
            this.dialogMock = new (MockBehavior.Strict);
            this.dbFactoryMock = new (MockBehavior.Strict);
            this.dbMock = new (MockBehavior.Strict);
            this.uowMock = new (MockBehavior.Strict);
            this.entityReaderMock = new (MockBehavior.Strict);
            this.backupWriterMock = new (MockBehavior.Strict);
            this.toastNotifierMock = new ();
            this.accountsRepo = new ();
            this.transactionsRepo = new ();
            this.categoriesRepo = new ();
            this.exchangeRatesRepo = new ();

            this.dbFactoryMock.Setup(x => x.CreateDatabase())
                .Returns(this.dbMock.Object);
        }

        [Fact]
        public async Task AbankCommand_OpenWizard_AddNewTransaction()
        {
            await this.SetupDbManual();

            var path = Path.Combine(Environment.CurrentDirectory, "Assets", "abank.pdf");
            this.csvMock.SetupGet(x => x.ReportType).Returns(ReportType.Pdf);
            this.dialogMock.Setup(x => x.OpenFileDialogAsync("pdf")).ReturnsAsync(path);

            SetupImportWizard(path);

            var vm = await this.GetLoadedFinancistoVM();
            await vm.ImportCommand.ExecuteAsync(this.csvMock.Object);

            this.trMock.VerifyAll();
            this.dbMock.Verify();
            this.toastNotifierMock.Verify(x => x.ShowMessage(string.Format(LocalizationService.Instance.import_result, 1)), Times.Once);
        }

        [Theory]
        [AutoMoqData]
        public async Task AddTransaction_NewItem_AddedToRepo(
            Transaction transaction,
            TransactionDto output)
        {
            await this.SetupDbManual();
            this.SetupWizardRepos();
            this.SetupRepo(new Mock<IBaseRepository<Payee>>());
            this.SetupRepo(new Mock<IBaseRepository<BlotterTransactions>>());
            this.trMock = new (MockBehavior.Strict);
            this.dbMock.Setup(x => x.GetOrCreateTransactionAsync(0)).ReturnsAsync(transaction);

            this.dbMock.Setup(x => x.GetSubTransactionsAsync(It.IsAny<int>())).ReturnsAsync(Array.Empty<Transaction>());

            this.dbMock.Setup(x => x.InsertOrUpdateAsync(It.IsAny<List<Transaction>>())).Returns(Task.CompletedTask);
            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.dbMock.Setup(x => x.RebuildAccountBalanceAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
            this.uowMock.Setup(x => x.Dispose());
            this.dialogMock.Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<TransactionDialogVM>(), 640, 440, LocalizationService.Instance.transaction))
                .ReturnsAsync(output);

            var vm = this.GetBlotterVM();
            await vm.AddCommand.ExecuteAsync();

            this.trMock.VerifyAll();
            this.dbMock.Verify();
        }

        [Theory]
        [AutoMoqData]
        public async Task AddTransfer_NewTransfer_AddedToDb(
            Transaction transaction,
            TransferDto output)
        {
            await this.SetupDbManual();
            this.SetupRepo(new Mock<IBaseRepository<Account>>());
            this.SetupRepo(new Mock<IBaseRepository<BlotterTransactions>>());

            this.dbMock.Setup(x => x.GetOrCreateTransactionAsync(0)).ReturnsAsync(transaction);
            this.dbMock.Setup(x => x.RebuildAccountBalanceAsync(It.IsAny<int>())).Returns(Task.CompletedTask);
            this.dbMock.Setup(x => x.InsertOrUpdateAsync(It.IsAny<Transaction[]>())).Returns(Task.CompletedTask);

            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.Dispose());
            this.dialogMock.Setup(x => x.ShowDialogAsync<TransferDialog>(It.IsAny<TransferDialogVM>(), 480, 440, It.IsAny<string>()))
                .ReturnsAsync(output);

            var vm = this.GetBlotterVM();
            await vm.AddTransferCommand.ExecuteAsync();

            this.dbMock.Verify();
            this.dbMock.Verify(x => x.RebuildAccountBalanceAsync(It.IsAny<int>()), Times.Exactly(2));
        }

        [Fact]
        public void Constructor_NoParameters_MenuItemsCreated()
        {
            var vm = this.GetFinancistoVM();

            Assert.Null(vm.CurrentPage);
            Assert.NotEmpty(vm.ItemsTop);
            Assert.NotEmpty(vm.ItemsBottom);
            Assert.Contains(vm.ItemsTop, x => x.ModelType == typeof(BlotterModel));
            Assert.Contains(vm.ItemsTop, x => x.ModelType == typeof(LocationModel));
            Assert.Contains(vm.ItemsTop, x => x.ModelType == typeof(PayeeModel));
            Assert.Contains(vm.ItemsTop, x => x.ModelType == typeof(ProjectModel));
        }

        [Theory]
        [AutoMoqData]
        public async Task Locations_AddRaised_NewItemAdded(LocationDto result)
        {
            await this.SetupDbManual();
            var location = new Location() { Id = 0 };
            Location[] actual = null;

            this.dbMock.Setup(x => x.GetOrCreateAsync<Location>(0)).ReturnsAsync(location);

            this.dbMock.Setup(x => x.InsertOrUpdateAsync(It.IsAny<Location[]>())).Callback<IEnumerable<Location>>((x) => { actual = x.ToArray(); }).Returns(Task.CompletedTask);
            this.dialogMock.Setup(x => x.ShowDialogAsync<LocationDialog>(It.IsAny<LocationDialogVM>(), 400, 300, LocalizationService.Instance.location)).ReturnsAsync(result);
            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.Dispose()).Verifiable();
            this.locMock = new Mock<IBaseRepository<Location>>();
            this.SetupRepo(this.locMock);

            var vm = this.GetLocationsVM();
            await vm.AddCommand.ExecuteAsync();

            this.dbMock.Verify();
            this.dbMock.Verify(x => x.GetOrCreateAsync<Location>(0), Times.Once);
            this.dbMock.Verify(x => x.InsertOrUpdateAsync(It.IsAny<Location[]>()), Times.Once);
            Assert.Equal(result.Address, actual[0].Address);
            Assert.Equal(result.IsActive, actual[0].IsActive);
            Assert.Equal(result.Title, actual[0].Title);
            Assert.Equal(0, actual[0].Id);
            Assert.Equal(0, actual[0].Count);
        }

        [Fact]
        public async Task Locations_AddRaisedCancelClicked_NoNewItemAdded()
        {
            var location = new Location() { Id = 0 };

            this.dbMock.Setup(x => x.GetOrCreateAsync<Location>(0)).ReturnsAsync(location);

            this.dialogMock.Setup(x => x.ShowDialogAsync<LocationDialog>(It.IsAny<LocationDialogVM>(), 400, 300, LocalizationService.Instance.location)).ReturnsAsync((object)null);

            this.locMock = new ();
            this.SetupRepo(this.locMock);

            var vm = this.GetLocationsVM();
            await vm.AddCommand.ExecuteAsync();

            this.dbMock.VerifyAll();
            this.dbMock.Verify(x => x.GetOrCreateAsync<Location>(0), Times.Once);
            this.dbMock.Verify(x => x.InsertOrUpdateAsync<Location>(It.IsAny<Location[]>()), Times.Never);
        }

        [Fact]
        public async Task MenuNavigateCommand_ChangeCurrentPage_PropertiesUpdated()
        {
            await this.SetupDbManual();
            var vm = this.GetFinancistoVM();

            this.locMock = new Mock<IBaseRepository<Location>>();
            this.payeeMock = new Mock<IBaseRepository<Payee>>();
            this.projMock = new Mock<IBaseRepository<Project>>();

            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.GetRepository<BlotterTransactions>()).Returns(transactionsRepo.Object);
            this.uowMock.Setup(x => x.Dispose());

            this.SetupRepo(this.exchangeRatesRepo);
            this.SetupRepo(this.locMock);
            this.SetupRepo(this.projMock);
            this.SetupRepo(this.payeeMock);

            await vm.MenuNavigateCommand.ExecuteAsync(typeof(BlotterModel));
            Assert.True(vm.CurrentPage is BlotterPageVM);
            await vm.MenuNavigateCommand.ExecuteAsync(typeof(LocationModel));
            Assert.True(vm.CurrentPage is LocationsPageVM);
            await vm.MenuNavigateCommand.ExecuteAsync(typeof(ProjectModel));
            Assert.True(vm.CurrentPage is ProjectsPageVM);
            await vm.MenuNavigateCommand.ExecuteAsync(typeof(PayeeModel));
            Assert.True(vm.CurrentPage is PayeesPageVM);
            await vm.MenuNavigateCommand.ExecuteAsync(typeof(ExchangeRateModel));
            Assert.True(vm.CurrentPage is ExchangeRatesPageVM);
        }

        [Fact]
        public async Task MonoCommand_Cancel_NoTransactionsAdded()
        {
            await this.SetupDbManual();

            var csvPath = Path.Combine(Environment.CurrentDirectory, "Assets", "mono.ukr.csv");
            this.csvMock.SetupGet(x => x.ReportType).Returns(ReportType.Csv);
            this.dialogMock.Setup(x => x.OpenFileDialogAsync("csv")).ReturnsAsync(csvPath);

            SetupImportWizard(csvPath, wizardOutput: null);

            var vm = await this.GetLoadedFinancistoVM();
            await vm.ImportCommand.ExecuteAsync(this.csvMock.Object);

            this.dbMock.Verify(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>()), Times.Never);
            this.toastNotifierMock.Verify(x => x.ShowMessage(It.Is<string>(m => m.StartsWith("Imported"))), Times.Never);
        }

        [Fact]
        public async Task MonoCommand_DuplicatesFound_NoTransactionsAdded()
        {
            await this.SetupDbManual();
            var outputTransaction = new Transaction()
            {
                Id = 0,
                FromAmount = 100,
                CategoryId = 1,
                OriginalCurrencyId = 0,
                FromAccountId = 2,
                OriginalFromAmount = 0,
                DateTime = 1639121044000,
            };
            var findManyOutput = new List<Transaction> { outputTransaction };

            var csvPath = Path.Combine(Environment.CurrentDirectory, "Assets", "mono.ukr.csv");
            this.csvMock.SetupGet(x => x.ReportType).Returns(ReportType.Csv);
            this.dialogMock.Setup(x => x.OpenFileDialogAsync("csv")).ReturnsAsync(csvPath);

            SetupImportWizard(csvPath, wizardOutput: findManyOutput, existingTransactions: findManyOutput);

            var vm = await this.GetLoadedFinancistoVM();
            await vm.ImportCommand.ExecuteAsync(this.csvMock.Object);

            this.trMock.VerifyAll();
            this.dbMock.Verify(x => x.AddTransactionsAsync(It.Is<List<Transaction>>(t => t.Count == 0)), Times.Once);
            this.toastNotifierMock.Verify(x => x.ShowMessage(string.Format(LocalizationService.Instance.import_result_with_duplicates, 0, 1)), Times.Once);
        }

        [Fact]
        public async Task MonoCommand_OpenWizard_AddNewTransaction()
        {
            await this.SetupDbManual();

            var path = Path.Combine(Environment.CurrentDirectory, "Assets", "mono.ukr.csv");
            this.csvMock.SetupGet(x => x.ReportType).Returns(ReportType.Csv);
            this.dialogMock.Setup(x => x.OpenFileDialogAsync("csv")).ReturnsAsync(path);

            SetupImportWizard(path);

            var vm = await this.GetLoadedFinancistoVM();
            await vm.ImportCommand.ExecuteAsync(this.csvMock.Object);

            this.trMock.VerifyAll();
            this.dbMock.Verify();
            this.toastNotifierMock.Verify(x => x.ShowMessage(string.Format(LocalizationService.Instance.import_result, 1)), Times.Once);
        }

        [Theory]
        [AutoMoqData]
        public async Task OpenBackup_ParseBackup_ImportEntities(
            string backupPath,
            IEnumerable<Entity> entities,
            BackupVersion backupVersion,
            Dictionary<string, List<string>> entityColumnsOrder)
        {
            await this.SetupDbManual();
            var vm = this.GetFinancistoVM();
            this.entityReaderMock.Setup(x => x.ParseBackupFileAsync(backupPath)).ReturnsAsync((entities, backupVersion, entityColumnsOrder));

            this.dbMock.Setup(x => x.ImportEntitiesAsync(entities)).Returns(Task.CompletedTask).Verifiable();
            this.dbMock.Setup(x => x.Dispose());
            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.Dispose()).Verifiable();
            this.SetupRepo(this.transactionsRepo);

            await vm.OpenBackup(backupPath);

            this.dbMock.Verify();

            Assert.True(vm.CurrentPage is BlotterPageVM);
            Assert.Equal(backupPath, vm.OpenBackupPath);
        }

        [Theory]
        [AutoMoqData]
        public async Task OpenTransaction_Cancel_NoUpdateTransaction(BlotterModel eventArgs, Transaction transaction)
        {
            eventArgs.CategoryId = -1;
            eventArgs.ParentId = 0; // an ordinary row, not a part of a split
            await this.SetupDbManual();
            this.SetupWizardRepos();
            this.SetupRepo(new Mock<IBaseRepository<Payee>>());
            this.trMock = new (MockBehavior.Strict);
            this.dbMock.Setup(x => x.GetOrCreateTransactionAsync(eventArgs.Id)).ReturnsAsync(transaction);
            this.dbMock.Setup(x => x.GetSubTransactionsAsync(It.IsAny<int>())).ReturnsAsync(Array.Empty<Transaction>());

            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.Dispose());
            this.dialogMock.Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<TransactionDialogVM>(), 640, 440, LocalizationService.Instance.transaction))
                .ReturnsAsync((object)null);

            var vm = this.GetBlotterVM();
            vm.SelectedValue = eventArgs;
            await vm.EditCommand.ExecuteAsync();

            this.trMock.VerifyAll();
            this.dbMock.Verify();
        }

        [Theory]
        [AutoMoqData]
        public async Task OpenTransaction_ExistingTransaction_UpdateTransaction(
            BlotterModel eventArgs,
            Transaction transaction,
            AccountFilterModel account,
            IEnumerable<Transaction> subTransactions)
        {
            eventArgs.CategoryId = -1;
            eventArgs.ParentId = 0; // an ordinary row, not a part of a split
            await this.SetupDbManual();
            var output = new TransactionDto(transaction, subTransactions);
            output.FromAccount = account;

            this.SetupWizardRepos();
            this.SetupRepo(new Mock<IBaseRepository<Payee>>());
            this.SetupRepo(new Mock<IBaseRepository<BlotterTransactions>>());
            this.trMock = new (MockBehavior.Strict);
            this.dbMock.Setup(x => x.GetOrCreateTransactionAsync(eventArgs.Id))
                .ReturnsAsync(transaction);
            this.dbMock.Setup(x => x.GetOrCreateAsync<Transaction>(It.IsAny<int>()))
                .ReturnsAsync(new Transaction());
            this.dbMock.Setup(x => x.GetSubTransactionsAsync(It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<Transaction>());

            this.dbMock.Setup(x => x.InsertOrUpdateAsync(It.IsAny<List<Transaction>>()))
                .Returns(Task.CompletedTask);
            this.dbMock.Setup(x => x.RebuildAccountBalanceAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);
            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.Dispose());
            this.dialogMock.Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<TransactionDialogVM>(), 640, 440, LocalizationService.Instance.transaction))
                .ReturnsAsync(output);

            var vm = this.GetBlotterVM();
            vm.SelectedValue = eventArgs;
            await vm.EditCommand.ExecuteAsync();

            this.trMock.VerifyAll();
            this.dbMock.Verify();
            this.dbMock.Verify(x => x.GetOrCreateAsync<Transaction>(It.IsAny<int>()), Times.Exactly(subTransactions.Count()));
        }

        [Theory]
        [AutoMoqData]
        public async Task OpenTransaction_NoSubTransaction_UpdateTransaction(
            BlotterModel eventArgs,
            Transaction transaction,
            TransactionDto output)
        {
            eventArgs.CategoryId = -1;
            eventArgs.ParentId = 0; // an ordinary row, not a part of a split
            eventArgs.ToAccountId = 0;

            await this.SetupDbManual();
            this.SetupWizardRepos();
            this.SetupRepo(new Mock<IBaseRepository<Payee>>());
            this.SetupRepo(new Mock<IBaseRepository<BlotterTransactions>>());
            this.trMock = new (MockBehavior.Strict);
            this.dbMock.Setup(x => x.GetOrCreateTransactionAsync(eventArgs.Id))
                .ReturnsAsync(transaction);

            this.dbMock.Setup(x => x.GetSubTransactionsAsync(It.IsAny<int>()))
                .ReturnsAsync(Array.Empty<Transaction>());

            this.dbMock.Setup(x => x.InsertOrUpdateAsync(It.IsAny<List<Transaction>>()))
                .Returns(Task.CompletedTask);
            this.dbMock.Setup(x => x.CreateUnitOfWork())
                .Returns(this.uowMock.Object);
            this.dbMock.Setup(x => x.RebuildAccountBalanceAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);
            this.uowMock.Setup(x => x.Dispose());
            this.dialogMock.Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<TransactionDialogVM>(), 640, 440, LocalizationService.Instance.transaction))
                .ReturnsAsync(output);

            var vm = this.GetBlotterVM();
            vm.SelectedValue = eventArgs;
            await vm.EditCommand.ExecuteAsync();

            this.trMock.VerifyAll();
            this.dbMock.Verify();
        }

        [Theory]
        [AutoMoqData]
        public async Task OpenTransfer_Cancel_NoUpdateTransaction(BlotterModel eventArgs, Transaction transaction)
        {
            eventArgs.FromAccountId = 1;
            eventArgs.ToAccountId = 2;
            eventArgs.CategoryId = 0;
            eventArgs.ParentId = 0; // an ordinary row, not a part of a split

            this.SetupRepo(new Mock<IBaseRepository<Account>>());

            this.dbMock.Setup(x => x.GetOrCreateTransactionAsync(eventArgs.Id)).ReturnsAsync(transaction);

            this.uowMock.Setup(x => x.Dispose());
            this.dialogMock.Setup(x => x.ShowDialogAsync<TransferDialog>(It.IsAny<TransferDialogVM>(), 480, 440, It.IsAny<string>()))
                .ReturnsAsync((object)null);

            var vm = this.GetBlotterVM();
            vm.SelectedValue = eventArgs;
            await vm.EditCommand.ExecuteAsync();

            this.dbMock.VerifyAll();
        }

        [Theory]
        [AutoMoqData]
        public async Task OpenTransfer_ExistingTransaction_UpdateTransaction(
            BlotterModel eventArgs,
            Transaction transaction,
            TransferDto output)
        {
            eventArgs.FromAccountId = 1;
            eventArgs.ToAccountId = 2;
            eventArgs.CategoryId = 0;
            eventArgs.ParentId = 0; // an ordinary row, not a part of a split

            await this.SetupDbManual();

            this.SetupRepo(new Mock<IBaseRepository<Account>>());
            this.SetupRepo(new Mock<IBaseRepository<BlotterTransactions>>());
            this.trMock = new (MockBehavior.Strict);
            this.dbMock.Setup(x => x.GetOrCreateTransactionAsync(eventArgs.Id)).ReturnsAsync(transaction);

            this.dbMock.Setup(x => x.InsertOrUpdateAsync(It.IsAny<Transaction[]>())).Returns(Task.CompletedTask);
            this.dbMock.Setup(x => x.RebuildAccountBalanceAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.Dispose());

            this.dialogMock.Setup(x => x.ShowDialogAsync<TransferDialog>(It.IsAny<TransferDialogVM>(), 480, 440, It.IsAny<string>()))
                .ReturnsAsync(output);

            var vm = this.GetBlotterVM();
            vm.SelectedValue = eventArgs;
            await vm.EditCommand.ExecuteAsync();

            this.trMock.VerifyAll();
            this.dbMock.Verify();
            this.dbMock.Verify(x => x.RebuildAccountBalanceAsync(It.IsAny<int>()), Times.Exactly(2));
        }

        [Theory]
        [AutoMoqData]
        public async Task Projects_AddRaised_NewItemAdded(TagDto result)
        {
            var location = new Project() { Id = 0 };
            Project[] actual = null;

            this.dbMock.Setup(x => x.GetOrCreateAsync<Project>(0)).ReturnsAsync(location);
            await this.SetupDbManual();
            this.dbMock.Setup(x => x.InsertOrUpdateAsync(It.IsAny<Project[]>())).Callback<IEnumerable<Project>>((x) => { actual = x.ToArray(); }).Returns(Task.CompletedTask);
            this.dialogMock.Setup(x => x.ShowDialogAsync<TagDialog>(It.IsAny<TagDialogVM>(), 180, 300, LocalizationService.Instance["project"])).ReturnsAsync(result);

            this.projMock = new ();
            this.SetupRepo(this.projMock);

            var vm = this.GetProjectsVM();
            await vm.AddCommand.ExecuteAsync();

            this.dbMock.Verify(x => x.GetOrCreateAsync<Project>(0), Times.Once);
            this.dbMock.Verify(x => x.InsertOrUpdateAsync(It.IsAny<Project[]>()), Times.Once);
            Assert.Equal(result.IsActive, actual[0].IsActive);
            Assert.Equal(result.Title, actual[0].Title);
            Assert.Equal(0, actual[0].Id);
        }

        [Fact]
        public async Task Projects_AddRaisedCancelClicked_NoNewItemAdded()
        {
            var location = new Project() { Id = 0 };

            this.dbMock.Setup(x => x.GetOrCreateAsync<Project>(0)).ReturnsAsync(location);

            this.dialogMock.Setup(x => x.ShowDialogAsync<TagDialog>(It.IsAny<TagDialogVM>(), 180, 300, LocalizationService.Instance["project"])).ReturnsAsync((object)null);

            this.projMock = new ();
            this.SetupRepo(this.projMock);

            var vm = this.GetProjectsVM();
            await vm.AddCommand.ExecuteAsync();

            this.dbMock.VerifyAll();
            this.dbMock.Verify(x => x.GetOrCreateAsync<Project>(0), Times.Once);
            this.dbMock.Verify(x => x.InsertOrUpdateAsync(It.IsAny<Project[]>()), Times.Never);
        }

        [Fact]
        public async Task PumbCommand_OpenWizard_AddNewTransaction()
        {
            await this.SetupDbManual();

            var path = Path.Combine(Environment.CurrentDirectory, "Assets", "pumb.pdf");
            this.csvMock.SetupGet(x => x.ReportType).Returns(ReportType.Pdf);
            this.dialogMock.Setup(x => x.OpenFileDialogAsync("pdf")).ReturnsAsync(path);

            SetupImportWizard(path);

            var vm = await this.GetLoadedFinancistoVM();
            await vm.ImportCommand.ExecuteAsync(this.csvMock.Object);

            this.trMock.VerifyAll();
            this.dbMock.Verify();
            this.toastNotifierMock.Verify(x => x.ShowMessage(string.Format(LocalizationService.Instance.import_result, 1)), Times.Once);
        }

        [Theory]
        [AutoMoqData]
        public async Task SaveBackup_ReadEntitiesFromDb_ExportEntities(
            string backupPath)
        {
            var vm = await this.GetLoadedFinancistoVM();
            this.backupWriterMock.Setup(x => x.GenerateBackupAsync(It.IsAny<List<Entity>>(), backupPath, It.IsAny<BackupVersion>(), It.IsAny<Dictionary<string, List<string>>>()))
                .Returns(Task.CompletedTask)
                .Verifiable();
            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.budgetMock = new ();
            this.trAMock = new ();
            this.currMock = new ();
            this.locMock = new ();
            this.payeeMock = new ();
            this.projMock = new ();
            this.tagMock = new ();
            this.trMock = new ();
            this.adMock = new ();
            this.caMock = new ();
            this.cccdMock = new ();
            this.smsMock = new ();

            this.SetupRepo(this.accountsRepo);
            this.SetupRepo(this.trMock);
            this.SetupRepo(this.categoriesRepo);
            this.SetupRepo(this.exchangeRatesRepo);
            this.SetupRepo(this.budgetMock);
            this.SetupRepo(this.trAMock);
            this.SetupRepo(this.currMock);
            this.SetupRepo(this.locMock);
            this.SetupRepo(this.payeeMock);
            this.SetupRepo(this.projMock);
            this.SetupRepo(this.tagMock);
            this.SetupRepo(this.adMock);
            this.SetupRepo(this.caMock);
            this.SetupRepo(this.cccdMock);
            this.SetupRepo(this.smsMock);

            this.uowMock.Setup(x => x.Dispose()).Verifiable();

            await vm.SaveBackup(backupPath);

            this.dbMock.VerifyAll();
            this.uowMock.VerifyAll();
            this.entityReaderMock.VerifyAll();
        }

        [Theory]
        [AutoMoqData]
        public async Task SaveBackupAsDb_ExecuteCommand_DBQueryExecuted(string backupPath)
        {
            var vm = await this.GetLoadedFinancistoVM();
            this.dbMock.Setup(x => x.SaveAsFile(backupPath)).Returns(Task.CompletedTask).Verifiable();
            this.dialogMock.Setup(x => x.SaveFileDialogAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(backupPath).Verifiable();

            await vm.SaveBackupAsDbCommand.ExecuteAsync();

            this.dbMock.VerifyAll();
            this.dialogMock.VerifyAll();
            this.toastNotifierMock.Verify(x => x.ShowMessage(string.Format(LocalizationService.Instance.saved_message, backupPath)), Times.Once);
        }

        [Fact]
        public async Task CheckForUpdates_NullUpdateService_ReturnsGracefully()
        {
            var vm = this.GetFinancistoVM(); // updateService is null

            await vm.CheckForUpdatesAsync();

            this.toastNotifierMock.Verify(x => x.ShowMessage(It.IsAny<string>()), Times.Never);
            this.toastNotifierMock.Verify(x => x.ShowWarning(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task OpenBackupCommand_DialogReturnsEmpty_NoBackupOpened()
        {
            this.dialogMock.Setup(x => x.OpenFileDialogAsync("backup")).ReturnsAsync(string.Empty);

            var vm = this.GetFinancistoVM();
            await vm.OpenBackupCommand.ExecuteAsync();

            this.entityReaderMock.Verify(x => x.ParseBackupFileAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SaveBackupCommand_DialogReturnsEmpty_NoBackupSaved()
        {
            this.dialogMock.Setup(x => x.SaveFileDialogAsync("backup", It.IsAny<string>())).ReturnsAsync(string.Empty);

            var vm = await this.GetLoadedFinancistoVM();
            await vm.SaveBackupCommand.ExecuteAsync();

            this.backupWriterMock.Verify(
                x => x.GenerateBackupAsync(It.IsAny<List<Entity>>(), It.IsAny<string>(), It.IsAny<BackupVersion>(), It.IsAny<Dictionary<string, List<string>>>()),
                Times.Never);
        }

        [Fact]
        public async Task MenuNavigateCommand_AccountModel_SetsCurrentPage()
        {
            await this.SetupDbManual();

            var accountRepoMock = new Mock<IBaseRepository<Account>>();
            accountRepoMock.Setup(x => x.FindManyAndProjectAsync(
                It.IsAny<Expression<Func<Account, bool>>>(),
                It.IsAny<Expression<Func<Account, AccountModel>>>(),
                It.IsAny<Expression<Func<Account, object>>[]>()))
                .ReturnsAsync(new List<AccountModel>());

            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.GetRepository<Account>()).Returns(accountRepoMock.Object);
            this.uowMock.Setup(x => x.Dispose());

            var vm = this.GetFinancistoVM();
            await vm.MenuNavigateCommand.ExecuteAsync(typeof(AccountModel));

            Assert.True(vm.CurrentPage is AccountsPageVM);
        }

        [Fact]
        public async Task MenuNavigateCommand_CurrencyModel_SetsCurrentPage()
        {
            await this.SetupDbManual();

            var vm = this.GetFinancistoVM();
            await vm.MenuNavigateCommand.ExecuteAsync(typeof(CurrencyModel));

            Assert.True(vm.CurrentPage is CurrenciesPageVM);
        }

        [Fact]
        public async Task MenuNavigateCommand_CategoryTreeModel_SetsCurrentPage()
        {
            await this.SetupDbManual();

            var vm = this.GetFinancistoVM();
            await vm.MenuNavigateCommand.ExecuteAsync(typeof(CategoryTreeModel));

            Assert.True(vm.CurrentPage is CategoriesPageVM);
        }

        [Fact]
        public async Task MenuNavigateCommand_SmsTemplateModel_SetsCurrentPage()
        {
            // The page refresh sets DbManual up, which is static: don't depend on an earlier test having done it.
            await this.SetupDbManual();
            var smsRepo = new Mock<IBaseRepository<SmsTemplate>>();
            smsRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<SmsTemplate>());
            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.GetRepository<SmsTemplate>()).Returns(smsRepo.Object);
            this.uowMock.Setup(x => x.Dispose());
            var vm = this.GetFinancistoVM();
            await vm.MenuNavigateCommand.ExecuteAsync(typeof(SmsTemplateModel));

            Assert.True(vm.CurrentPage is SmsTemplatesPageVM);
        }

        private BlotterPageVM GetBlotterVM()
        {
            // Read right before a transaction or transfer dialog opens (shows the account balance).
            this.dbMock.Setup(x => x.GetLastRunningBalancesAsync()).ReturnsAsync(new Dictionary<int, long>());
            return new BlotterPageVM(this.dbMock.Object, this.dialogMock.Object);
        }

        private LocationsPageVM GetLocationsVM() => new LocationsPageVM(this.dbMock.Object, this.dialogMock.Object);

        private ProjectsPageVM GetProjectsVM() => new ProjectsPageVM(this.dbMock.Object, this.dialogMock.Object);

        /// <summary>Import and save commands stay disabled until a backup is open, so open an empty one first.</summary>
        private async Task<MainWindowVM> GetLoadedFinancistoVM()
        {
            await this.SetupDbManual();

            var entities = new List<Entity>();
            this.entityReaderMock.Setup(x => x.ParseBackupFileAsync("test.backup"))
                .ReturnsAsync((entities, new BackupVersion(), new Dictionary<string, List<string>>()));
            this.dbMock.Setup(x => x.ImportEntitiesAsync(It.IsAny<IEnumerable<Entity>>())).Returns(Task.CompletedTask);
            this.dbMock.Setup(x => x.Dispose());
            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.Dispose());
            this.uowMock.Setup(x => x.GetRepository<BlotterTransactions>()).Returns(this.transactionsRepo.Object);

            var vm = this.GetFinancistoVM();
            await vm.OpenBackup("test.backup");
            return vm;
        }

        private MainWindowVM GetFinancistoVM() => new MainWindowVM(this.dialogMock.Object, this.dbFactoryMock.Object, this.entityReaderMock.Object, this.backupWriterMock.Object, this.toastNotifierMock.Object, null);

        private async Task SetupDbManual()
        {
            this.dbMock.Setup(x => x.ExecuteQuery<AccountFilterModel>(It.IsAny<string>())).ReturnsAsync(new List<AccountFilterModel>() { new AccountFilterModel() });
            this.dbMock.Setup(x => x.ExecuteQuery<CategoryModel>(It.IsAny<string>())).ReturnsAsync(new List<CategoryModel>() { new CategoryModel() });
            this.dbMock.Setup(x => x.ExecuteQuery<CurrencyModel>(It.IsAny<string>())).ReturnsAsync(new List<CurrencyModel>() { new CurrencyModel() });
            this.dbMock.Setup(x => x.ExecuteQuery<PayeeModel>(It.IsAny<string>())).ReturnsAsync(new List<PayeeModel>() { new PayeeModel() });
            this.dbMock.Setup(x => x.ExecuteQuery<ProjectModel>(It.IsAny<string>())).ReturnsAsync(new List<ProjectModel>() { new ProjectModel() });
            this.dbMock.Setup(x => x.ExecuteQuery<TagModel>(It.IsAny<string>())).ReturnsAsync(new List<TagModel>() { new TagModel() });
            this.dbMock.Setup(x => x.ExecuteQuery<YearMonths>(It.IsAny<string>())).ReturnsAsync(new List<YearMonths>() { new YearMonths() });
            this.dbMock.Setup(x => x.ExecuteQuery<Years>(It.IsAny<string>())).ReturnsAsync(new List<Years>() { new Years() });
            this.dbMock.Setup(x => x.ExecuteQuery<LocationModel>(It.IsAny<string>())).ReturnsAsync(new List<LocationModel>() { new LocationModel() });

            // DbManual reads the import rules from sms_template
            var rulesRepo = new Mock<IBaseRepository<SmsTemplate>>();
            rulesRepo.Setup(x => x.GetAllAsync()).ReturnsAsync(new List<SmsTemplate>());
            this.uowMock.Setup(x => x.GetRepository<SmsTemplate>()).Returns(rulesRepo.Object);
            this.uowMock.Setup(x => x.Dispose());
            this.dbMock.Setup(x => x.CreateUnitOfWork()).Returns(this.uowMock.Object);

            DbManual.ResetAllDatabaseManuals();
            await DbManual.SetupAsync(this.dbMock.Object);
        }

        private void SetupImportWizard(string path)
        {
            var output = new List<Transaction>()
            {
                new Transaction()
                {
                    Id = 0,
                    FromAmount = 100,
                    CategoryId = 1,
                    OriginalCurrencyId = 0,
                    FromAccountId = 2,
                    OriginalFromAmount = 0,
                    DateTime = 1639121044000,
                },
            };

            this.SetupImportWizard(path, output);
        }

        /// <param name="wizardOutput">What the wizard returns; null means it was cancelled.</param>
        /// <param name="existingTransactions">Transactions already in the database (duplicate candidates).</param>
        private void SetupImportWizard(string path, List<Transaction> wizardOutput, List<Transaction> existingTransactions = null)
        {
            this.dialogMock.Setup(x => x.ShowWizardAsync(It.IsAny<MonoWizardVM>()))
                .ReturnsAsync(wizardOutput);
            this.SetupWizardRepos();
            this.SetupRepo(new Mock<IBaseRepository<BlotterTransactions>>());
            this.transactionsRepo.Setup(x => x.FindManyAndProjectAsync(
                    It.IsAny<Expression<Func<BlotterTransactions, bool>>>(),
                    It.IsAny<Expression<Func<BlotterTransactions, BlotterModel>>>(),
                    It.IsAny<Expression<Func<BlotterTransactions, object>>[]>()))
                .ReturnsAsync(new List<BlotterModel>());
            this.trMock = new (MockBehavior.Strict);
            this.uowMock.Setup(x => x.GetRepository<Transaction>())
                .Returns(this.trMock.Object);
            this.dbMock.Setup(x => x.AddTransactionsAsync(It.IsAny<List<Transaction>>()))
                .Returns(Task.CompletedTask);
            this.trMock.Setup(x => x.FindManyAsync(It.IsAny<Expression<Func<Transaction, bool>>>()))
                .ReturnsAsync(existingTransactions ?? new List<Transaction>());
            this.dbMock.Setup(x => x.RebuildAccountBalanceAsync(It.IsAny<int>()))
                .Returns(Task.CompletedTask);
            this.dbMock.Setup(x => x.CreateUnitOfWork())
                .Returns(this.uowMock.Object);
            this.uowMock.Setup(x => x.Dispose());
            this.csvMock.Setup(x => x.ParseReport(path))
                .Returns(Array.Empty<BankTransaction>());
            this.csvMock.SetupGet(x => x.BankTitle).Returns(string.Empty);
        }

        private void SetupRepo<T>(Mock<IBaseRepository<T>> mock)
            where T : Entity
        {
            this.uowMock.Setup(x => x.GetRepository<T>()).Returns(mock.Object);
            mock.Setup(x => x.GetAllAsync(It.IsAny<Expression<Func<T, object>>[]>())).ReturnsAsync(new List<T>());
        }

        private void SetupWizardRepos()
        {
            this.SetupRepo(new Mock<IBaseRepository<Account>>());
            this.SetupRepo(new Mock<IBaseRepository<Currency>>());
            this.SetupRepo(new Mock<IBaseRepository<Location>>());
            this.SetupRepo(new Mock<IBaseRepository<Category>>());
            this.SetupRepo(new Mock<IBaseRepository<Project>>());
        }
    }
}
