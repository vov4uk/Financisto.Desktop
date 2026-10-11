namespace Financisto.Desktop.Tests.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Financisto.Desktop.ViewModels.Pages;
    using Financisto.Desktop.Views.Dialogs;
    using Moq;
    using Xunit;

    /// <summary>The blotter's Template button (Android's "new from template") and Save as template.</summary>
    public class BlotterTemplatesTests : TemplatesTestBase
    {
        private BlotterPageVM vm;
        private TransactionDialogVM transactionDialog;
        private TransferDialogVM transferDialog;
        private TemplatesDialogVM templatesDialog;

        [Fact]
        public async Task TemplateCommand_NothingPicked_OpensNothingAndStoresNothing()
        {
            await this.SetupAsync(selection: null);
            var before = (await this.AllTransactionsAsync()).Count;

            await this.vm.TemplateCommand.ExecuteAsync();

            Assert.NotNull(this.templatesDialog);
            Assert.Null(this.transactionDialog);
            Assert.Null(this.transferDialog);
            Assert.Equal(before, (await this.AllTransactionsAsync()).Count);
        }

        [Fact]
        public async Task TemplateCommand_ShowsTheTemplatesToPickFrom()
        {
            await this.SetupAsync(selection: null);

            await this.vm.TemplateCommand.ExecuteAsync();

            Assert.True(this.templatesDialog.IsSelectMode);
            Assert.Equal(new[] { this.groceriesId, this.toBankId, this.coffeeId }, this.templatesDialog.Templates.Select(x => x.Id));
        }

        [Fact]
        public async Task TemplateCommand_ATransactionTemplate_OpensTheTransactionDialogFilledFromIt()
        {
            await this.SetupAsync(selection: null);
            this.selection = new TemplateSelection(this.coffeeId, 1);
            var before = DateTime.Now.AddMinutes(-1);

            await this.vm.TemplateCommand.ExecuteAsync();

            Assert.Null(this.transferDialog);
            var dto = this.transactionDialog.Transaction;
            Assert.Equal(0, dto.Id); // a new transaction, not the template
            Assert.False(dto.IsTemplate);
            Assert.Null(dto.TemplateName);
            Assert.Equal(this.cashId, dto.FromAccountId);
            Assert.Equal(this.foodId, dto.CategoryId);
            Assert.Equal(-350, dto.RealFromAmount);
            Assert.InRange(dto.DateTime, before, DateTime.Now.AddMinutes(1)); // dated now, not when the template was made
        }

        [Fact]
        public async Task TemplateCommand_Saved_AddsANewTransactionAndKeepsTheTemplate()
        {
            await this.SetupAsync(selection: null);
            this.selection = new TemplateSelection(this.coffeeId, 3); // x3
            var before = await this.AllTransactionsAsync();

            await this.vm.TemplateCommand.ExecuteAsync();

            var after = await this.AllTransactionsAsync();
            var created = Assert.Single(after.Where(x => before.All(b => b.Id != x.Id)));
            Assert.Equal(0, created.IsTemplate);
            Assert.Equal(-1050, created.FromAmount);
            Assert.Equal(this.foodId, created.CategoryId);

            var template = after.Single(x => x.Id == this.coffeeId);
            Assert.Equal(1, template.IsTemplate);
            Assert.Equal(-350, template.FromAmount);

            // the new transaction moved the balance (-5.00 - 10.50), and it is on the blotter
            Assert.Equal(-1550, (await this.AccountAsync(this.cashId)).TotalAmount);
            Assert.Contains(this.vm.Entities, x => x.Id == created.Id);
            Assert.DoesNotContain(this.vm.Entities, x => x.Id == this.coffeeId); // the templates are not blotter rows
        }

        [Fact]
        public async Task TemplateCommand_ATransferTemplate_OpensTheTransferDialog_AndSavesATransfer()
        {
            await this.SetupAsync(selection: null);
            this.selection = new TemplateSelection(this.toBankId, 2);

            await this.vm.TemplateCommand.ExecuteAsync();

            Assert.Null(this.transactionDialog);
            Assert.Equal(0, this.transferDialog.Transfer.Id);
            Assert.False(this.transferDialog.Transfer.IsTemplate);
            Assert.Equal(this.cashId, this.transferDialog.Transfer.FromAccountId);
            Assert.Equal(this.bankId, this.transferDialog.Transfer.ToAccountId);

            var created = (await this.AllTransactionsAsync()).Single(x => x.IsTemplate == 0 && x.ToAccountId == this.bankId);
            Assert.Equal(-20000, created.FromAmount);
            Assert.Equal(20000, created.ToAmount);
            Assert.Equal(20000, (await this.AccountAsync(this.bankId)).TotalAmount);
        }

        [Fact]
        public async Task TemplateCommand_ASplitTemplate_OpensTheDialogWithItsParts_AndSavesTheWholeSplit()
        {
            await this.SetupAsync(selection: null);
            this.selection = new TemplateSelection(this.groceriesId, 1);

            await this.vm.TemplateCommand.ExecuteAsync();

            Assert.True(this.transactionDialog.Transaction.IsSplitCategory);
            Assert.Equal(2, this.transactionDialog.Transaction.SubTransactions.Count);

            var all = await this.AllTransactionsAsync();
            var parent = all.Single(x => x.IsTemplate == 0 && x.CategoryId == -1);
            var parts = all.Where(x => x.ParentId == parent.Id).ToList();
            Assert.Equal(2, parts.Count);
            Assert.All(parts, x => Assert.Equal(0, x.IsTemplate));
            Assert.Equal(-3000, parts.Sum(x => x.FromAmount));
            // the template's own parts are still there
            Assert.Equal(2, all.Count(x => x.ParentId == this.groceriesId && x.IsTemplate == 1));
        }

        [Fact]
        public async Task TemplateCommand_TheDialogIsCancelled_StoresNothing()
        {
            await this.SetupAsync(selection: null);
            this.selection = new TemplateSelection(this.coffeeId, 1);
            this.returnEditedDialog = false;
            var before = (await this.AllTransactionsAsync()).Count;

            await this.vm.TemplateCommand.ExecuteAsync();

            Assert.NotNull(this.transactionDialog);
            Assert.Equal(before, (await this.AllTransactionsAsync()).Count);
            Assert.Equal(-500, (await this.AccountAsync(this.cashId)).TotalAmount);
        }

        [Fact]
        public async Task TemplateCommand_TheTemplateWasDeleted_DoesNothing()
        {
            await this.SetupAsync(selection: null);
            this.selection = new TemplateSelection(9999, 1);

            await this.vm.TemplateCommand.ExecuteAsync();

            Assert.Null(this.transactionDialog);
            Assert.Null(this.transferDialog);
        }

        [Fact]
        public async Task SaveAsTemplateCommand_NeedsASelectedRow()
        {
            await this.SetupAsync(selection: null);

            Assert.False(this.vm.SaveAsTemplateCommand.CanExecute());

            await this.vm.RefreshDataCommand.ExecuteAsync();
            this.vm.SelectedValue = this.vm.Entities.Single();

            Assert.True(this.vm.SaveAsTemplateCommand.CanExecute());
        }

        [Fact]
        public async Task SaveAsTemplateCommand_MakesATemplateOfTheRow_TellsTheUser_AndLeavesTheBlotterAlone()
        {
            await this.SetupAsync(selection: null);
            await this.vm.RefreshDataCommand.ExecuteAsync();
            this.vm.SelectedValue = this.vm.Entities.Single(); // the ordinary -5.00

            await this.vm.SaveAsTemplateCommand.ExecuteAsync();

            var templates = await new TemplateStore(this.db).GetTemplatesAsync();
            Assert.Equal(4, templates.Count);
            var added = templates.Single(x => x.Id != this.coffeeId && x.Id != this.toBankId && x.Id != this.groceriesId);
            Assert.Equal(-500, added.FromAmount);
            Assert.Equal("Food", added.CategoryTitle);

            this.notifier.Verify(x => x.ShowMessage(LocalizationService.Instance.save_as_template_success), Times.Once);

            await this.vm.RefreshDataCommand.ExecuteAsync();
            Assert.Equal(this.ordinaryId, this.vm.Entities.Single().Id);
            Assert.Equal(-500, (await this.AccountAsync(this.cashId)).TotalAmount);
        }

        private TemplateSelection selection;
        private bool returnEditedDialog = true;
        private Mock<IToastNotifierWrapper> notifier;

        private async Task SetupAsync(TemplateSelection selection)
        {
            await base.SetupAsync();
            this.selection = selection;

            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TemplatesDialog>(It.IsAny<DialogBaseVM>(), 560, 760, LocalizationService.Instance.transaction_templates))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    this.templatesDialog = (TemplatesDialogVM)dialog;
                    return Task.FromResult<object>(this.selection);
                });

            // saving the dialog returns what it was opened with
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransactionDialog>(It.IsAny<DialogBaseVM>(), 640, 440, LocalizationService.Instance.transaction))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    this.transactionDialog = (TransactionDialogVM)dialog;
                    return Task.FromResult<object>(this.returnEditedDialog ? this.transactionDialog.Transaction : null);
                });
            this.dialogMock
                .Setup(x => x.ShowDialogAsync<TransferDialog>(It.IsAny<DialogBaseVM>(), 480, 440, LocalizationService.Instance.transfer))
                .Returns<DialogBaseVM, double, double, string>((dialog, _, _, _) =>
                {
                    this.transferDialog = (TransferDialogVM)dialog;
                    return Task.FromResult<object>(this.returnEditedDialog ? this.transferDialog.Transfer : null);
                });

            this.notifier = new Mock<IToastNotifierWrapper>();
            this.vm = new BlotterPageVM(this.db, this.dialogMock.Object) { Notifier = this.notifier.Object };
        }
    }
}
