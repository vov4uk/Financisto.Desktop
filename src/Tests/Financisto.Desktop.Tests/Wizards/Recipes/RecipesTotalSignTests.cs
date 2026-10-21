namespace Financisto.Desktop.Tests.Wizards.Recipes
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Wizards.Recipes;
    using Xunit;

    /// <summary>
    /// A row is an expense (negative amount) or, for a returned item, a credit (positive amount). The total of page 1 is their sum with its sign, as page 2 sums it:
    /// it used to be forced negative, so a refund receipt showed the wrong total and a Diff twice as big as it was.
    /// </summary>
    public sealed class RecipesTotalSignTests : IDisposable
    {
        private const string Purchase = "Mleko B 1.000 x 5,49 5,49";
        private const string Return = "Zwrot towaru B 2.000 x 5,49 -10,98";
        private const string Deposit = "Zwrot kaucji brak 1.000 x 0,50 -0,50";

        private readonly string dir = Path.Combine(Path.GetTempPath(), "Financisto.Desktop.Tests", "total-sign", Guid.NewGuid().ToString("N"));

        public RecipesTotalSignTests()
        {
            Directory.CreateDirectory(this.dir);
            DbManual.SetupTests(new List<CategoryModel>());
            DbManual.SetupTests(new List<AccountFilterModel>());
            DbManual.SetupTests(new List<CurrencyModel>());
        }

        public void Dispose()
        {
            DbManual.ResetAllDatabaseManuals();
            Directory.Delete(this.dir, true);
        }

        private static Page1VM Calculate(double total, params string[] lines)
        {
            var vm = new Page1VM(total) { Text = string.Join(Environment.NewLine, lines) };
            vm.CalculateCurrentAmount();
            return vm;
        }

        [Fact]
        public void Expenses_TotalIsNegative()
        {
            var vm = Calculate(-5.49, Purchase);

            Assert.Equal(-549, vm.Amounts.Single().FromAmount);
            Assert.Equal(-5.49, vm.CalculatedAmount);
            Assert.Equal(0.0, vm.Diff);
        }

        [Fact]
        public void OnlyReturnedItems_RowsAreCreditsAndTheTotalIsPositive()
        {
            var vm = Calculate(0.5, Deposit);

            Assert.Equal(50, vm.Amounts.Single().FromAmount);
            Assert.Equal(0.5, vm.CalculatedAmount);
            Assert.Equal(0.0, vm.Diff);
        }

        [Fact]
        public void ReturnsExceedPurchases_NetCreditIsPositive()
        {
            // 5.49 spent, 10.98 given back: 5.49 comes back to the account
            var vm = Calculate(5.49, Purchase, Return);

            Assert.Equal(new long[] { -549, 1098 }, vm.Amounts.Select(row => row.FromAmount));
            Assert.Equal(5.49, vm.CalculatedAmount);
            Assert.Equal(0.0, vm.Diff);
        }

        [Fact]
        public void PurchasesExceedReturns_NetExpenseIsNegative()
        {
            var vm = Calculate(-7.0, "Chleb A 1.000 x 10,00 10,00", "Zwrot A 1.000 x 3,00 -3,00");

            Assert.Equal(-7.0, vm.CalculatedAmount);
            Assert.Equal(0.0, vm.Diff);
        }

        [Fact]
        public void ReturnCancelsThePurchase_ZeroIsNotNegativeZero()
        {
            var vm = Calculate(0.0, "Chleb A 1.000 x 5,00 5,00", "Zwrot A 1.000 x 5,00 -5,00");

            Assert.Equal(0.0, vm.CalculatedAmount);
            Assert.False(double.IsNegative(vm.CalculatedAmount)); // -0.0 prints as "-0,00"
            Assert.DoesNotContain("-0", vm.FormattedTotal);
            Assert.False(double.IsNegative(vm.Diff));
        }

        [Fact]
        public void MarkedAmountLines_ACreditLineIsPositiveToo()
        {
            var vm = Calculate(0.5, "Discount -0.5 A");

            Assert.Equal(50, vm.Amounts.Single().FromAmount);
            Assert.Equal(0.5, vm.CalculatedAmount);
            Assert.Equal(0.0, vm.Diff);
        }

        [Fact]
        public void MarkedAmountLines_ExpensesStillNegative()
        {
            var vm = Calculate(-75.0, "Beer Baltyka 0 50,0 A", "Discount -0.5 A", "Milk 25.5 A");

            Assert.Equal(-75.0, vm.CalculatedAmount);
            Assert.Equal(0.0, vm.Diff);
        }

        [Fact]
        public void NoRows_TotalStaysZero()
        {
            var vm = Calculate(-5.49, "nothing that looks like a receipt row");

            Assert.Equal(0.0, vm.CalculatedAmount);
            Assert.False(double.IsNegative(vm.CalculatedAmount));
        }

        [Theory]
        [InlineData(-5.49, Purchase)]
        [InlineData(0.5, Deposit)]
        [InlineData(5.49, Purchase + "\n" + Return)]
        [InlineData(-7.0, "Chleb A 1.000 x 10,00 10,00\nZwrot A 1.000 x 3,00 -3,00")]
        public void Page2_SumsTheRowsTheWayPage1Did(double total, string text)
        {
            var vm = new RecipesVM(total, new RecipeCategoryMemory(Path.Combine(this.dir, "RecipeCategories.json")));
            var page1 = (Page1VM)vm.CurrentPage;
            page1.Text = text.Replace("\n", Environment.NewLine);

            vm.MoveNextCommand.Execute();
            var page2 = (Page2VM)vm.CurrentPage;
            page2.TotalCommand.Execute();

            Assert.Equal(total, page1.CalculatedAmount, 2);
            Assert.Equal(page1.CalculatedAmount, page2.CalculatedAmount, 2);
            Assert.Equal(0.0, page2.Diff, 2);
        }
    }
}
