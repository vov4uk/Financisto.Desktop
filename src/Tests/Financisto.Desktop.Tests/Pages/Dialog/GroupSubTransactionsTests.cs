namespace Financisto.Desktop.Tests.ViewModel.Dialog
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Financisto.Desktop.Views.Dialogs;
    using Moq;
    using Xunit;

    /// <summary>The "group by category" button of the transaction dialog: parts of one category become one part with the sum.</summary>
    public class GroupSubTransactionsTests
    {
        // ---- TransactionDto.GroupSubTransactionsByCategory

        [Fact]
        public void Group_TwoPartsOfOneCategory_OnePartWithTheSum()
        {
            var parent = Parent(Part(1, -1000), Part(1, -500));

            var merged = parent.GroupSubTransactionsByCategory();

            Assert.Equal(1, merged);
            var part = Assert.IsType<TransactionDto>(Assert.Single(parent.SubTransactions));
            Assert.Equal(1, part.CategoryId);
            Assert.Equal(-1500, part.RealFromAmount);
            Assert.True(part.IsAmountNegative);
            Assert.Equal(-1500, part.FromAmount); // the list shows FromAmount: it must keep the minus (it did not, and "-3,99" + "-3,99" showed as "7,98")
        }

        [Fact]
        public void Group_PartsMadeLikeTheRecipesWizardDoes_ListAmountKeepsItsMinus()
        {
            // RecipesVM.TransformMonoTransaction: FromAmount negative cents, IsAmountNegative, no original amount
            TransactionDto FromWizard(int category, long cents) => new TransactionDto
            {
                CategoryId = category,
                FromAmount = cents,
                IsAmountNegative = cents < 0,
                OriginalFromAmount = 0,
                OriginalCurrencyId = 0,
            };
            var parent = Parent(FromWizard(10, -399), FromWizard(10, -399), FromWizard(20, -289), FromWizard(30, -499), FromWizard(30, -899));

            parent.GroupSubTransactionsByCategory();

            var parts = parent.SubTransactions.Cast<TransactionDto>().ToList();
            Assert.Equal(new long[] { -798, -289, -1398 }, parts.Select(part => part.FromAmount));
            Assert.All(parts, part => Assert.True(part.IsAmountNegative));
            Assert.Equal(new long[] { -798, -289, -1398 }, parts.Select(part => part.RealFromAmount));
        }

        [Fact]
        public void Group_PartsWithAbsoluteFromAmount_StillMerged()
        {
            var parent = Parent(Part(1, -1000, signedAmount: false), Part(1, -500, signedAmount: false), Part(2, -200, signedAmount: false));

            parent.GroupSubTransactionsByCategory();

            var merged = (TransactionDto)parent.SubTransactions[0];
            Assert.Equal(-1500, merged.RealFromAmount);
            Assert.Equal(-1500, merged.FromAmount);
            Assert.True(merged.IsAmountNegative);
            Assert.Equal(-200, parent.SubTransactions[1].RealFromAmount); // the part that was not merged is as it was
        }

        [Fact]
        public void Group_PartsInAnotherCurrency_OriginalAmountSummed()
        {
            DbManual.SetupTests(new List<CurrencyModel> { new CurrencyModel { Id = 1, Name = "PLN" }, new CurrencyModel { Id = 2, Name = "UAH" } });
            DbManual.SetupTests(new List<AccountFilterModel> { new AccountFilterModel { Id = 5, CurrencyId = 1 } });
            try
            {
                TransactionDto Foreign(long amount) => new TransactionDto
                {
                    CategoryId = 1,
                    FromAccountId = 5,
                    OriginalCurrencyId = 2,
                    FromAmount = amount,
                    OriginalFromAmount = amount,
                    IsAmountNegative = amount < 0,
                };
                var first = Foreign(-1000);
                Assert.True(first.IsOriginalFromAmountVisible); // the amount of such a part is its original amount
                var parent = Parent(first, Foreign(-500));

                parent.GroupSubTransactionsByCategory();

                var part = (TransactionDto)Assert.Single(parent.SubTransactions);
                Assert.Equal(-1500, part.RealFromAmount);
                Assert.Equal(-1500, part.OriginalFromAmount);
                Assert.Equal(-1500, part.FromAmount);
            }
            finally
            {
                DbManual.ResetAllDatabaseManuals();
            }
        }

        [Fact]
        public void Group_AmountToSplit_DoesNotChange()
        {
            var parent = Parent(Part(1, -1000), Part(1, -500), Part(2, -200));
            var unsplit = parent.UnsplitAmount;
            var split = parent.SplitAmount;

            parent.GroupSubTransactionsByCategory();

            Assert.Equal(unsplit, parent.UnsplitAmount);
            Assert.Equal(split, parent.SplitAmount);
        }

        [Fact]
        public void Group_Incomes_StayIncomes()
        {
            var parent = Parent(Part(1, 1000), Part(1, 500));

            parent.GroupSubTransactionsByCategory();

            var part = (TransactionDto)Assert.Single(parent.SubTransactions);
            Assert.False(part.IsAmountNegative);
            Assert.Equal(1500, part.RealFromAmount);
            Assert.Equal(1500, part.FromAmount);
        }

        [Fact]
        public void Group_DifferentCategories_Untouched()
        {
            var parent = Parent(Part(1, -1000), Part(2, -500), Part(3, -200));

            var merged = parent.GroupSubTransactionsByCategory();

            Assert.Equal(0, merged);
            Assert.Equal(new long[] { -1000, -500, -200 }, parent.SubTransactions.Select(part => part.RealFromAmount));
        }

        [Fact]
        public void Group_PartsInterleaved_EachCategoryKeepsThePlaceOfItsFirstPart()
        {
            var parent = Parent(Part(1, -100), Part(2, -200), Part(1, -300), Part(3, -400), Part(2, -500));

            var merged = parent.GroupSubTransactionsByCategory();

            Assert.Equal(2, merged);
            var parts = parent.SubTransactions.Cast<TransactionDto>().ToList();
            Assert.Equal(new int?[] { 1, 2, 3 }, parts.Select(part => part.CategoryId));
            Assert.Equal(new long[] { -400, -700, -400 }, parts.Select(part => part.RealFromAmount));
        }

        [Fact]
        public void Group_FirstPartKeepsItsId_TheOthersAreGone()
        {
            var first = Part(1, -100, id: 11);
            var second = Part(1, -200, id: 12);
            var parent = Parent(first, second);

            parent.GroupSubTransactionsByCategory();

            Assert.Same(first, Assert.Single(parent.SubTransactions));
            Assert.Equal(11, first.Id);
        }

        [Fact]
        public void Group_OppositeSigns_NetAmountWithItsSign()
        {
            var parent = Parent(Part(1, -1000), Part(1, 400));

            parent.GroupSubTransactionsByCategory();

            var part = (TransactionDto)Assert.Single(parent.SubTransactions);
            Assert.Equal(-600, part.RealFromAmount);
            Assert.Equal(-600, part.FromAmount);
            Assert.True(part.IsAmountNegative);
        }

        [Fact]
        public void Group_NetsToPositive_BecomesAnIncome()
        {
            var parent = Parent(Part(1, -400), Part(1, 1000));

            parent.GroupSubTransactionsByCategory();

            var part = (TransactionDto)Assert.Single(parent.SubTransactions);
            Assert.Equal(600, part.RealFromAmount);
            Assert.Equal(600, part.FromAmount);
            Assert.False(part.IsAmountNegative);
        }

        [Fact]
        public void Group_PartsCancelOut_NoPartLeft()
        {
            var parent = Parent(Part(1, -1000), Part(1, 1000), Part(2, -300));

            var merged = parent.GroupSubTransactionsByCategory();

            Assert.Equal(2, merged);
            Assert.Equal(2, ((TransactionDto)Assert.Single(parent.SubTransactions)).CategoryId);
        }

        [Fact]
        public void Group_Notes_DifferentOnesJoinedOthersKept()
        {
            var parent = Parent(Part(1, -100, note: "milk"), Part(1, -100, note: null), Part(1, -100, note: " Milk "), Part(1, -100, note: "bread"));

            parent.GroupSubTransactionsByCategory();

            Assert.Equal("milk, bread", ((TransactionDto)Assert.Single(parent.SubTransactions)).Note);
        }

        [Fact]
        public void Group_NoNotes_NoteStaysEmpty()
        {
            var parent = Parent(Part(1, -100, note: null), Part(1, -100, note: " "));

            parent.GroupSubTransactionsByCategory();

            Assert.True(string.IsNullOrWhiteSpace(((TransactionDto)Assert.Single(parent.SubTransactions)).Note));
        }

        [Fact]
        public void Group_SameCategoryDifferentProject_NotMerged()
        {
            var parent = Parent(Part(1, -100, project: 5), Part(1, -200, project: 6), Part(1, -300, project: 5), Part(1, -400, project: null));

            var merged = parent.GroupSubTransactionsByCategory();

            Assert.Equal(1, merged);
            var parts = parent.SubTransactions.Cast<TransactionDto>().ToList();
            Assert.Equal(new int?[] { 5, 6, null }, parts.Select(part => part.ProjectId));
            Assert.Equal(new long[] { -400, -200, -400 }, parts.Select(part => part.RealFromAmount));
        }

        [Fact]
        public void Group_PartsWithoutCategory_GroupedTogether()
        {
            var noCategory = Part(0, -100);
            var nullCategory = Part(0, -200);
            nullCategory.CategoryId = null;
            var parent = Parent(noCategory, nullCategory);

            parent.GroupSubTransactionsByCategory();

            Assert.Equal(-300, Assert.Single(parent.SubTransactions).RealFromAmount);
        }

        [Fact]
        public void Group_SplitTransfers_LeftWhereTheyAre()
        {
            var transfer = new TransferDto { FromAmount = 700, IsAmountNegative = true };
            var parent = Parent(Part(1, -100), transfer, Part(1, -200));

            parent.GroupSubTransactionsByCategory();

            Assert.Equal(2, parent.SubTransactions.Count);
            Assert.IsType<TransactionDto>(parent.SubTransactions[0]);
            Assert.Same(transfer, parent.SubTransactions[1]);
            Assert.Equal(-300, parent.SubTransactions[0].RealFromAmount);
        }

        [Fact]
        public void Group_NothingToGroup_ReturnsZero()
        {
            Assert.Equal(0, Parent().GroupSubTransactionsByCategory());
            Assert.Equal(0, Parent(Part(1, -100)).GroupSubTransactionsByCategory());
        }

        [Fact]
        public void Group_RunTwice_SecondTimeChangesNothing()
        {
            var parent = Parent(Part(1, -100), Part(1, -200), Part(2, -300));
            parent.GroupSubTransactionsByCategory();

            Assert.Equal(0, parent.GroupSubTransactionsByCategory());
            Assert.Equal(2, parent.SubTransactions.Count);
        }

        [Theory]
        [InlineData(1, 1, true)]
        [InlineData(1, 2, false)]
        public void CanGroup_TwoParts_OnlyWhenTheSameCategory(int first, int second, bool expected)
        {
            Assert.Equal(expected, Parent(Part(first, -100), Part(second, -100)).CanGroupSubTransactions());
        }

        [Fact]
        public void CanGroup_SameCategoryTransfersOnly_False()
        {
            Assert.False(Parent(new TransferDto(), new TransferDto()).CanGroupSubTransactions());
        }

        // ---- the button

        [Fact]
        public void GroupCommand_NothingToGroup_Disabled()
        {
            var vm = CreateVm(Part(1, -100), Part(2, -200));

            Assert.False(vm.GroupSubTransactionsCommand.CanExecute());
        }

        [Fact]
        public void GroupCommand_PartAdded_EnabledAndRaisesCanExecuteChanged()
        {
            var vm = CreateVm(Part(1, -100));
            var raised = 0;
            vm.GroupSubTransactionsCommand.CanExecuteChanged += (_, _) => raised++;
            Assert.False(vm.GroupSubTransactionsCommand.CanExecute());

            vm.Transaction.SubTransactions.Add(Part(1, -200));

            Assert.True(raised > 0);
            Assert.True(vm.GroupSubTransactionsCommand.CanExecute());
        }

        [Fact]
        public void GroupCommand_PartRemoved_Disabled()
        {
            var vm = CreateVm(Part(1, -100), Part(1, -200));
            Assert.True(vm.GroupSubTransactionsCommand.CanExecute());

            vm.DeleteSubTransactionCommand.Execute(vm.Transaction.SubTransactions[0]);

            Assert.False(vm.GroupSubTransactionsCommand.CanExecute());
        }

        [Fact]
        public void GroupCommand_Execute_PartsMergedAndButtonDisabled()
        {
            var vm = CreateVm(Part(1, -1000), Part(1, -500), Part(2, -250));

            vm.GroupSubTransactionsCommand.Execute();

            Assert.Equal(2, vm.Transaction.SubTransactions.Count);
            Assert.Equal(-1500, vm.Transaction.SubTransactions[0].RealFromAmount);
            Assert.False(vm.GroupSubTransactionsCommand.CanExecute());
        }

        [Fact]
        public async Task GroupCommand_PartEditedIntoTheCategoryOfAnother_Enabled()
        {
            var dialog = new Mock<IDialogWrapper>(MockBehavior.Strict);
            var vm = CreateVm(dialog.Object, Part(1, -100), Part(2, -200));
            var second = (TransactionDto)vm.Transaction.SubTransactions[1];
            Assert.False(vm.GroupSubTransactionsCommand.CanExecute());
            dialog.Setup(d => d.ShowDialogAsync<SubTransactionDialog>(It.IsAny<SubTransactionDialogVM>(), 340, 340, LocalizationService.Instance.sub_transaction))
                .ReturnsAsync(Part(1, -200));

            await vm.EditSubTransactionCommand.ExecuteAsync(second);

            Assert.Equal(1, second.CategoryId);
            Assert.True(vm.GroupSubTransactionsCommand.CanExecute());
        }

        private static TransactionDialogVM CreateVm(params BaseTransactionDto[] parts) => CreateVm(new Mock<IDialogWrapper>(MockBehavior.Strict).Object, parts);

        private static TransactionDialogVM CreateVm(IDialogWrapper dialog, params BaseTransactionDto[] parts) => new TransactionDialogVM(Parent(parts), dialog);

        private static TransactionDto Parent(params BaseTransactionDto[] parts)
        {
            var parent = new TransactionDto { FromAmount = 100000, IsAmountNegative = true };
            foreach (var part in parts)
            {
                parent.SubTransactions.Add(part);
            }

            parent.RecalculateUnSplitAmount();
            return parent;
        }

        /// <param name="amount">With its sign: negative = money spent.</param>
        /// <param name="signedAmount">As the recipes wizard and the part dialog make a part: FromAmount keeps the sign (what the list shows).
        /// False: FromAmount is the absolute value and only IsAmountNegative has the sign (parts of a transaction saved long ago).</param>
        private static TransactionDto Part(int category, long amount, string note = null, int? project = null, int id = 0, bool signedAmount = true) => new TransactionDto
        {
            Id = id,
            CategoryId = category,
            FromAmount = signedAmount ? amount : System.Math.Abs(amount),
            IsAmountNegative = amount < 0,
            Note = note,
            ProjectId = project,
        };
    }
}
