namespace Financisto.Desktop.Tests.Pages
{
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Financisto.Desktop.ViewModels.Pages;
    using Financisto.Desktop.Views.Dialogs;
    using Moq;
    using Xunit;

    /// <summary>Only one currency can be the default one: saving a new default currency has to clear the flag of the old one.</summary>
    [Collection("Integration tests")]
    public class CurrenciesPageVMTest
    {
        private readonly Mock<IDialogWrapper> dialogMock = new();

        [Fact]
        public async Task Edit_SetAsDefault_OldDefaultCurrencyBecomesNonDefault()
        {
            using var db = await CreateDbAsync();
            ReturnsFromCurrencyDialog(await CreateDtoAsync(db, id: 2, isDefault: true));
            var vm = new CurrenciesPageVM(db, dialogMock.Object);
            await vm.RefreshDataCommand.ExecuteAsync();
            vm.SelectedValue = vm.Entities.Single(x => x.Id == 2);

            await vm.EditCommand.ExecuteAsync();

            Assert.Equal(new[] { 2 }, await GetDefaultIdsAsync(db));
            Assert.Equal(new[] { false, true }, vm.Entities.OrderBy(x => x.Id).Select(x => x.IsDefault));
        }

        [Fact]
        public async Task Add_NewDefaultCurrency_OldDefaultCurrencyBecomesNonDefault()
        {
            using var db = await CreateDbAsync();
            dialogMock
                .Setup(d => d.ShowDialogAsync<NewCurrencyDialog>(It.IsAny<DialogBaseVM>(), 400, 340, LocalizationService.Instance.currencies))
                .ReturnsAsync(new CurrencyTemplateItem(true, null, "new"));
            ReturnsFromCurrencyDialog(new CurrencyDto { Title = "Zloty", Name = "PLN", Symbol = "zl", IsDefault = true, Decimals = 2, DecimalSeparator = "'.'", GroupSeparator = "' '" });
            var vm = new CurrenciesPageVM(db, dialogMock.Object);

            await vm.AddCommand.ExecuteAsync();

            var ids = await GetDefaultIdsAsync(db);
            var zloty = Assert.Single(vm.Entities, x => x.Name == "PLN");
            Assert.Equal(new[] { zloty.Id.Value }, ids);
            Assert.Equal(3, vm.Entities.Count);
        }

        [Fact]
        public async Task Edit_NotDefault_KeepsTheDefaultCurrency()
        {
            using var db = await CreateDbAsync();
            ReturnsFromCurrencyDialog(await CreateDtoAsync(db, id: 2, isDefault: false));
            var vm = new CurrenciesPageVM(db, dialogMock.Object);
            await vm.RefreshDataCommand.ExecuteAsync();
            vm.SelectedValue = vm.Entities.Single(x => x.Id == 2);

            await vm.EditCommand.ExecuteAsync();

            Assert.Equal(new[] { 1 }, await GetDefaultIdsAsync(db));
        }

        [Fact]
        public async Task Edit_DefaultCurrencyStaysDefault_StaysTheOnlyDefault()
        {
            using var db = await CreateDbAsync();
            var dto = await CreateDtoAsync(db, id: 1, isDefault: true);
            dto.Title = "Hryvnia";
            ReturnsFromCurrencyDialog(dto);
            var vm = new CurrenciesPageVM(db, dialogMock.Object);
            await vm.RefreshDataCommand.ExecuteAsync();
            vm.SelectedValue = vm.Entities.Single(x => x.Id == 1);

            await vm.EditCommand.ExecuteAsync();

            Assert.Equal(new[] { 1 }, await GetDefaultIdsAsync(db));
            Assert.Equal("Hryvnia", vm.Entities.Single(x => x.Id == 1).Title);
        }

        [Fact]
        public async Task Edit_SavedThroughTheRealDialogVM_KeepsTheCurrencyFormat()
        {
            using var db = await CreateDbAsync();
            dialogMock
                .Setup(d => d.ShowDialogAsync<CurrencyDialog>(It.IsAny<DialogBaseVM>(), 440, 700, LocalizationService.Instance.currency))
                .Returns((DialogBaseVM context, double height, double width, string title) => Task.FromResult<object>(context.OnRequestSave()));
            var vm = new CurrenciesPageVM(db, dialogMock.Object);
            await vm.RefreshDataCommand.ExecuteAsync();
            vm.SelectedValue = vm.Entities.Single(x => x.Id == 1);

            await vm.EditCommand.ExecuteAsync();

            var hryvnia = vm.Entities.Single(x => x.Id == 1);
            Assert.Equal(("','", "' '"), (hryvnia.DecimalSeparator, hryvnia.GroupSeparator));
            Assert.Equal("2 450,00", 2450d.ToString("N", hryvnia.getFormat()));
        }

        private void ReturnsFromCurrencyDialog(CurrencyDto result) => dialogMock
            .Setup(d => d.ShowDialogAsync<CurrencyDialog>(It.IsAny<DialogBaseVM>(), 440, 700, LocalizationService.Instance.currency))
            .ReturnsAsync(result);

        private static async Task<CurrencyDto> CreateDtoAsync(IFinancistoDatabase db, int id, bool isDefault)
        {
            var dto = new CurrencyDto(await db.GetOrCreateAsync<Currency>(id));
            dto.IsDefault = isDefault;
            return dto;
        }

        private static async Task<int[]> GetDefaultIdsAsync(IFinancistoDatabase db)
        {
            using var uow = db.CreateUnitOfWork();
            var all = await uow.GetRepository<Currency>().GetAllAsync();
            return all.Where(x => x.IsDefault).Select(x => x.Id).OrderBy(x => x).ToArray();
        }

        private static async Task<IFinancistoDatabase> CreateDbAsync()
        {
            DbManual.ResetAllDatabaseManuals();
            var db = new FinancistoDatabaseFactory().CreateDatabase();
            await db.ImportEntitiesAsync(new Entity[]
            {
                new Currency { Id = 1, Name = "UAH", Title = "Ukrainian hryvnia", Symbol = "UAH", IsDefault = true, DecimalSeparator = "','", GroupSeparator = "' '" },
                new Currency { Id = 2, Name = "USD", Title = "United States dollar", Symbol = "$", DecimalSeparator = "'.'", GroupSeparator = "' '" },
            });
            await DbManual.SetupAsync(db);
            return db;
        }
    }
}
