namespace Financisto.Desktop.Tests.Pages
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Financisto.Desktop.Views.Dialogs;
    using Moq;
    using Xunit;

    /// <summary>The list of templates: picking one (Android's SelectTemplateFragment) and managing them (TemplatesListFragment).</summary>
    public class TemplatesDialogVMTests : TemplatesTestBase
    {
        private static string TemplateTitle => LocalizationService.Instance.transaction_template;

        [Fact]
        public async Task Select_ListsTheTemplatesNewestFirst_NothingSelected()
        {
            await this.SetupAsync();

            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object);

            Assert.True(vm.IsSelectMode);
            Assert.False(vm.HasNoTemplates);
            Assert.Equal(new[] { this.groceriesId, this.toBankId, this.coffeeId }, vm.Templates.Select(x => x.Id));
            Assert.Null(vm.SelectedTemplate);
            Assert.False(vm.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task Select_NoTemplates_SaysSo()
        {
            await this.SetupAsync();
            foreach (var id in new[] { this.groceriesId, this.toBankId, this.coffeeId })
            {
                await new TemplateStore(this.db).DeleteTemplateAsync(id);
            }

            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object);

            Assert.True(vm.HasNoTemplates);
        }

        [Theory]
        [InlineData("cof", "Coffee")]
        [InlineData("BANK", "To bank")]
        [InlineData("food", "Coffee")] // the category's name too
        [InlineData("  coffee ", "Coffee")]
        [InlineData("zzz", "")]
        public async Task Search_MatchesTheNameOrTheCategory_IgnoringCase(string text, string expectedNames)
        {
            await this.SetupAsync();
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object);

            vm.SearchText = text;

            Assert.Equal(expectedNames, string.Join(",", vm.Templates.Select(x => x.TemplateName)));
            Assert.Equal(expectedNames.Length == 0, vm.HasNoTemplates);
        }

        [Fact]
        public async Task Search_ClearedShowsAllAgain_AndAKeptSelectionStaysSelected()
        {
            await this.SetupAsync();
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object);
            vm.SelectedTemplate = vm.Templates.Single(x => x.Id == this.coffeeId);

            vm.SearchText = "cof";
            Assert.Equal(this.coffeeId, vm.SelectedTemplate.Id);

            vm.SearchText = "bank"; // coffee is filtered out, so the first match is selected instead
            Assert.Equal(this.toBankId, vm.SelectedTemplate.Id);

            vm.SearchText = "zzz";
            Assert.Null(vm.SelectedTemplate);

            vm.SearchText = string.Empty; // nothing to pick from without a search
            Assert.Equal(3, vm.Templates.Count);
            Assert.Null(vm.SelectedTemplate);
        }

        [Fact]
        public async Task Multiplier_StartsAtOne_AndNeverGoesBelowOne()
        {
            await this.SetupAsync();
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object);
            Assert.Equal("x1", vm.MultiplierText);

            vm.DecrementMultiplierCommand.Execute();
            Assert.Equal("x1", vm.MultiplierText);

            vm.IncrementMultiplierCommand.Execute();
            vm.IncrementMultiplierCommand.Execute();
            Assert.Equal("x3", vm.MultiplierText);

            vm.DecrementMultiplierCommand.Execute();
            Assert.Equal("x2", vm.MultiplierText);
        }

        [Fact]
        public async Task Select_ReturnsTheTemplateAndTheMultiplier()
        {
            await this.SetupAsync();
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object);
            object result = null;
            vm.RequestSave += (sender, _) => result = sender;

            vm.SelectedTemplate = vm.Templates.Single(x => x.Id == this.toBankId);
            vm.IncrementMultiplierCommand.Execute();
            Assert.True(vm.SaveCommand.CanExecute(null));
            vm.SaveCommand.Execute(null);

            Assert.Equal(new TemplateSelection(this.toBankId, 2), result);
        }

        [Fact]
        public async Task Select_DoubleClickPicks_WhileManagingItEdits()
        {
            await this.SetupAsync();

            var select = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object);
            var manage = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object, isManageMode: true);

            Assert.Same(select.SaveCommand, select.ActivateCommand);
            Assert.Same(manage.EditCommand, manage.ActivateCommand);
            Assert.True(manage.IsManageMode);
            Assert.False(manage.SaveCommand.CanExecute(null)); // managing doesn't pick
        }

        [Fact]
        public async Task EditTemplates_OpensTheManageList_ThenShowsWhatChangedThere()
        {
            await this.SetupAsync();
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object);
            TemplatesDialogVM manage = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TemplatesDialog>(It.IsAny<DialogBaseVM>(), 560, 760, LocalizationService.Instance.transaction_templates))
                .Returns<DialogBaseVM, double, double, string>(async (dialog, _, _, _) =>
                {
                    manage = (TemplatesDialogVM)dialog;
                    await new TemplateStore(this.db).DeleteTemplateAsync(this.coffeeId); // done in the other list
                    return null;
                });

            await vm.EditTemplatesCommand.ExecuteAsync();

            Assert.True(manage.IsManageMode);
            Assert.Equal(3, manage.Templates.Count); // read when it opened
            Assert.Equal(new[] { this.groceriesId, this.toBankId }, vm.Templates.Select(x => x.Id));
        }

        [Fact]
        public async Task Manage_Delete_AsksFirst_AndRemovesTheTemplateWithItsParts()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowMessageBoxAsync(LocalizationService.Instance.delete_template_confirm, It.IsAny<string>(), true))
                .ReturnsAsync(true);
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object, isManageMode: true);
            Assert.False(vm.DeleteCommand.CanExecute());

            vm.SelectedTemplate = vm.Templates.Single(x => x.Id == this.groceriesId);
            Assert.True(vm.DeleteCommand.CanExecute());
            await vm.DeleteCommand.ExecuteAsync();

            Assert.DoesNotContain(vm.Templates, x => x.Id == this.groceriesId);
            var left = await this.AllTransactionsAsync();
            Assert.DoesNotContain(left, x => x.Id == this.groceriesId || x.ParentId == this.groceriesId);
            Assert.Contains(left, x => x.Id == this.ordinaryId);
        }

        [Fact]
        public async Task Manage_Delete_NotConfirmed_KeepsTheTemplate()
        {
            await this.SetupAsync();
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object, isManageMode: true);

            vm.SelectedTemplate = vm.Templates.Single(x => x.Id == this.coffeeId);
            await vm.DeleteCommand.ExecuteAsync();

            Assert.Equal(3, vm.Templates.Count);
            Assert.Contains(await this.AllTransactionsAsync(), x => x.Id == this.coffeeId);
        }

        [Fact]
        public async Task Manage_AddTransactionTemplate_StoresATemplate_ThatMovesNoMoney()
        {
            await this.SetupAsync();
            TransactionDialogVM opened = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<DialogBaseVM>(), 640, 440, TemplateTitle))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    opened = (TransactionDialogVM)dialog;
                    var dto = opened.Transaction;
                    dto.FromAccountId = this.cashId;
                    dto.TemplateName = "  Lunch ";
                    dto.CategoryId = this.foodId;
                    dto.FromAmount = 1200;
                    dto.IsAmountNegative = true;
                    return Task.FromResult<object>(dto);
                });
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object, isManageMode: true);

            await vm.AddTransactionCommand.ExecuteAsync();

            Assert.True(opened.Transaction.IsTemplate);
            Assert.False(opened.Transaction.IsDateTimeEditable); // Android disables the date of a template
            Assert.Equal(0, opened.Transaction.Id);

            var added = (await this.AllTransactionsAsync()).Single(x => x.TemplateName == "Lunch"); // trimmed
            Assert.Equal(1, added.IsTemplate);
            Assert.Equal(-1200, added.FromAmount);
            Assert.Equal(this.cashId, added.FromAccountId);
            Assert.Equal(this.foodId, added.CategoryId);

            Assert.Equal(4, vm.Templates.Count);
            Assert.Contains(vm.Templates, x => x.Id == added.Id);
            Assert.Equal(-500, (await this.AccountAsync(this.cashId)).TotalAmount);
        }

        [Fact]
        public async Task Manage_AddTransferTemplate_StoresATransferTemplate()
        {
            await this.SetupAsync();
            TransferDialogVM opened = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransferDialog>(It.IsAny<DialogBaseVM>(), 540, 440, LocalizationService.Instance.transfer_template))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    opened = (TransferDialogVM)dialog;
                    var dto = opened.Transfer;
                    dto.FromAccount = DbManual.Account.Single(x => x.Id == this.cashId);
                    dto.FromAccountId = this.cashId;
                    dto.ToAccount = DbManual.Account.Single(x => x.Id == this.bankId);
                    dto.ToAccountId = this.bankId;
                    dto.FromAmount = 2500;
                    dto.TemplateName = "Top up";
                    return Task.FromResult<object>(dto);
                });
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object, isManageMode: true);

            await vm.AddTransferCommand.ExecuteAsync();

            Assert.True(opened.Transfer.IsTemplate);
            var added = (await this.AllTransactionsAsync()).Single(x => x.TemplateName == "Top up");
            Assert.Equal(1, added.IsTemplate);
            Assert.Equal(-2500, added.FromAmount);
            Assert.Equal(2500, added.ToAmount);
            Assert.Equal(this.bankId, added.ToAccountId);
            Assert.Equal(0, (await this.AccountAsync(this.bankId)).TotalAmount);
        }

        [Fact]
        public async Task Manage_AddTemplate_DialogCancelled_StoresNothing()
        {
            await this.SetupAsync();
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object, isManageMode: true);
            var before = (await this.AllTransactionsAsync()).Count;

            await vm.AddTransactionCommand.ExecuteAsync();
            await vm.AddTransferCommand.ExecuteAsync();

            Assert.Equal(before, (await this.AllTransactionsAsync()).Count);
        }

        [Fact]
        public async Task Manage_EditTemplate_KeepsItATemplate_ChangesTheName_AndTheBalancesStay()
        {
            await this.SetupAsync();
            TransactionDialogVM opened = null;
            string nameWhenOpened = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<DialogBaseVM>(), 640, 440, TemplateTitle))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    opened = (TransactionDialogVM)dialog;
                    nameWhenOpened = opened.Transaction.TemplateName;
                    opened.Transaction.TemplateName = "Espresso";
                    return Task.FromResult<object>(opened.Transaction);
                });
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object, isManageMode: true);

            vm.SelectedTemplate = vm.Templates.Single(x => x.Id == this.coffeeId);
            await vm.EditCommand.ExecuteAsync();

            Assert.Equal("Coffee", nameWhenOpened); // it opened with the template's own data
            Assert.Equal(this.coffeeId, opened.Transaction.Id);
            var coffee = (await this.AllTransactionsAsync()).Single(x => x.Id == this.coffeeId);
            Assert.Equal("Espresso", coffee.TemplateName);
            Assert.Equal(1, coffee.IsTemplate);
            Assert.Equal(-350, coffee.FromAmount);
            Assert.Equal("Espresso", vm.Templates.Single(x => x.Id == this.coffeeId).TemplateName);
            Assert.Equal(-500, (await this.AccountAsync(this.cashId)).TotalAmount);
        }

        [Fact]
        public async Task Manage_EditTransferTemplate_OpensTheTransferDialog()
        {
            await this.SetupAsync();
            TransferDialogVM opened = null;
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransferDialog>(It.IsAny<DialogBaseVM>(), 540, 440, LocalizationService.Instance.transfer_template))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    opened = (TransferDialogVM)dialog;
                    return Task.FromResult<object>(null);
                });
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object, isManageMode: true);

            vm.SelectedTemplate = vm.Templates.Single(x => x.Id == this.toBankId);
            await vm.EditCommand.ExecuteAsync();

            Assert.Equal("To bank", opened.Transfer.TemplateName);
            Assert.Equal(this.bankId, opened.Transfer.ToAccountId);
        }

        [Fact]
        public async Task Manage_EditSplitTemplate_ItsPartsStayTemplateRows()
        {
            await this.SetupAsync();
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<DialogBaseVM>(), 640, 440, TemplateTitle))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    var dto = ((TransactionDialogVM)dialog).Transaction;
                    Assert.Equal(2, dto.SubTransactions.Count);
                    dto.TemplateName = "Shopping";
                    return Task.FromResult<object>(dto);
                });
            var vm = await TemplatesDialogVM.CreateAsync(this.db, this.dialogMock.Object, isManageMode: true);
            var before = await this.AllTransactionsAsync();

            vm.SelectedTemplate = vm.Templates.Single(x => x.Id == this.groceriesId);
            await vm.EditCommand.ExecuteAsync();

            var after = await this.AllTransactionsAsync();
            Assert.Equal(before.Count, after.Count);
            Assert.Equal("Shopping", after.Single(x => x.Id == this.groceriesId).TemplateName);
            var parts = after.Where(x => x.ParentId == this.groceriesId).ToList();
            Assert.Equal(2, parts.Count);
            Assert.All(parts, x => Assert.Equal(1, x.IsTemplate));
            Assert.Equal(-500, (await this.AccountAsync(this.cashId)).TotalAmount);
        }
    }
}
