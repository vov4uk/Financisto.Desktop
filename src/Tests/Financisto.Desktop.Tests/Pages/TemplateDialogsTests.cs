namespace Financisto.Desktop.Tests.Pages
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Xunit;

    /// <summary>What the transaction and transfer dialogs do differently for a template: a name field, no date, and no amount required.</summary>
    public class TemplateDialogsTests : TemplatesTestBase
    {
        [Fact]
        public async Task TransactionDialog_ATemplate_NeedsAName_ButNoAmount()
        {
            await this.SetupAsync();
            var template = await this.db.GetOrCreateTransactionAsync(0);
            template.IsTemplate = 1;
            template.FromAccountId = this.cashId;
            var vm = new TransactionDialogVM(new TransactionDto(template, Array.Empty<Transaction>()), this.dialogMock.Object);

            Assert.True(vm.Transaction.IsTemplate);
            Assert.Equal(0, vm.Transaction.FromAmount);
            Assert.False(vm.SaveCommand.CanExecute(null)); // no name yet

            var changes = 0;
            vm.SaveCommand.CanExecuteChanged += (_, _) => changes++;
            vm.Transaction.TemplateName = "Rent";

            Assert.True(changes > 0);
            Assert.True(vm.SaveCommand.CanExecute(null)); // an amount can wait until the template is used

            vm.Transaction.TemplateName = "   ";
            Assert.False(vm.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task TransactionDialog_AnOrdinaryTransaction_StillNeedsAnAmount_AndHasNoNameField()
        {
            await this.SetupAsync();
            var ordinary = await this.db.GetOrCreateTransactionAsync(0);
            ordinary.FromAccountId = this.cashId;
            var vm = new TransactionDialogVM(new TransactionDto(ordinary, Array.Empty<Transaction>()), this.dialogMock.Object);

            Assert.False(vm.Transaction.IsTemplate);
            Assert.True(vm.Transaction.IsDateTimeEditable);
            Assert.False(vm.SaveCommand.CanExecute(null));

            vm.Transaction.FromAmount = 100;
            Assert.True(vm.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task TransferDialog_ATemplate_NeedsAName()
        {
            await this.SetupAsync();
            var template = await this.db.GetOrCreateTransactionAsync(this.toBankId);
            var vm = new TransferDialogVM(new TransferDto(template));

            Assert.True(vm.Transfer.IsTemplate);
            Assert.Equal("To bank", vm.Transfer.TemplateName);
            Assert.True(vm.SaveCommand.CanExecute(null));

            vm.Transfer.TemplateName = string.Empty;
            Assert.False(vm.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task TransferDialog_AnOrdinaryTransfer_NeedsNoName()
        {
            await this.SetupAsync();
            var transfer = await this.db.GetOrCreateTransactionAsync(0);
            transfer.FromAccountId = this.cashId;
            transfer.ToAccountId = this.bankId;
            var vm = new TransferDialogVM(new TransferDto(transfer));

            Assert.False(vm.Transfer.IsTemplate);
            Assert.True(vm.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task DateAndTime_AreLockedForATemplate_AndForAPartOfASplit()
        {
            await this.SetupAsync();

            var template = new TransactionDto(await this.db.GetOrCreateTransactionAsync(this.coffeeId));
            var transfer = new TransferDto(await this.db.GetOrCreateTransactionAsync(this.toBankId));
            var ordinary = new TransactionDto(await this.db.GetOrCreateTransactionAsync(this.ordinaryId));
            var part = new TransferDto { IsSubTransaction = true };

            Assert.False(template.IsDateTimeEditable);
            Assert.False(transfer.IsDateTimeEditable);
            Assert.True(ordinary.IsDateTimeEditable);
            Assert.False(part.IsDateTimeEditable);
        }

        [Fact]
        public async Task ThePartsOfASplitTemplate_AreNotTemplatesThemselvesInTheDialog()
        {
            await this.SetupAsync();
            var parent = await this.db.GetOrCreateTransactionAsync(this.groceriesId);
            var parts = await this.db.GetSubTransactionsAsync(this.groceriesId);

            var dto = new TransactionDto(parent, parts);

            Assert.True(dto.IsTemplate);
            Assert.All(dto.SubTransactions, x => Assert.False(x.IsTemplate)); // only the parent has the name
        }

        [Fact]
        public async Task MapTransaction_OnlyATemplateTakesTheName_AndTrimsIt()
        {
            await this.SetupAsync();
            var template = await this.db.GetOrCreateTransactionAsync(this.coffeeId);
            var dto = new TransactionDto(template, Array.Empty<Transaction>()) { TemplateName = "  Latte " };

            MapperHelper.MapTransaction(dto, template);

            Assert.Equal("Latte", template.TemplateName);
            Assert.Equal(1, template.IsTemplate); // the flag is the entity's own, the DTO doesn't change it

            var ordinary = await this.db.GetOrCreateTransactionAsync(this.ordinaryId);
            var ordinaryDto = new TransactionDto(ordinary, Array.Empty<Transaction>()) { TemplateName = "ignored" };
            MapperHelper.MapTransaction(ordinaryDto, ordinary);
            Assert.Null(ordinary.TemplateName);
            Assert.Equal(0, ordinary.IsTemplate);
        }
    }
}
