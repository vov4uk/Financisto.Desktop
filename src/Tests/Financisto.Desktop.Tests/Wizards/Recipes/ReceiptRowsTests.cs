namespace Financisto.Desktop.Tests.Wizards.Recipes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Avalonia.Controls.Documents;
    using Avalonia.Media;
    using Financisto.Common.Entities;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Wizards.RecipesWizard.View;
    using Financisto.Desktop.Wizards.RecipesWizard.ViewModel;
    using Xunit;

    /// <summary>The receipt printed as a table (Biedronka): <c>Name PTU quantity x price value</c>, <c>Rabat</c> rows, the net amount on its own line.</summary>
    public class ReceiptRowsTests
    {
        /// <summary>
        /// A real receipt as the Windows OCR read it from the PDF (scanned, two pages; the shop's address and the card details left out), noise included:
        /// "Wartoéé", "SosyDoPizzyGBe11230", a name wrapped onto two lines ("papryKa 5toaKa" ... "C.7erw"), a lost line break in "Suma PLN 248 / ,".
        /// The items cost 248.34, the sum on the receipt.
        /// </summary>
        internal const string BiedronkaReceipt = @"Biedronka
Codziennie niskie ceny
10.10.2026 12:34
NIEFISKALNY
Nazwa PTU 110"" Cena Wartoéé
SokJabtkRivPet11 c 6.000 x 2,99 17,94
Rabat -3,60
14,34
But Plastik kaucja brak 6.000 x 0,50 3,00
GalarWawelCzekolaLu A 0.435 x 39,90 17,36
Rabat -5,25
12,11
Cuk Kr6wkAWawelLuz A 0.235 x 28,90 6,79
Cebula Czerw Opa c 1.000 x 3,49 3,49
Kalafior luz c 0.960 x 8,99 8,63
PinsaGustoBe11230g c 4.000 x 13,59 54,36
Rabat -10,94
43,42
cukier trzc 0,5kg B 1.000 x 5,49 5,49
Rabat -1,20
4,29
cukier trzc 0,5kg B 1.000 x 5,49 5,49
Rabat -1,20
4,29
SosyDoPizzyGBe11230 B 1.000 x 4,99 4,99
SosyDoPizzyGBe11230 B 1.000 x 4,99 4,99
SSojAFlavours500ml B 1.000 x 8,49 8,49
Banan Luz c 1.010 x 6,99 7,06
Rabat -4,04
3,02
Marchew Luz c 0.510 x 3,99 2,03
papryKa 5toaKa
c 0.425 x 12,99 5,52
C.7erw
Limonka szt c 2.000 x 1,99 3,98
Rabat -2,00
1,98
6360/13/4667/10.10.2026 Strona 1 z 2
Mieso Z Uda 500g c 1.000 x 10,99 10,99
Mieso Z Uda 500g c 1.000 x 10,99 10,99
SerPleéCamNat120g c 1.000 x 6,99 6,99
SerRadamWedzSwia2 c 1.000 x 8,49 8,49
TwarP6ftH<raj500g c 1.000 x 7,99 7,99
TwarP6fttKraj500g c 1.000 x 7,99 7,99
szynl<aprosautt01UUg c 1.000 x 10,49 10,49
Rabat -2,63
7,86
szynKayrosautt01Uug
c 1.000 x 10,49 10,49
Rabat -2,64
7,85
CiasFrancuskie300g c 1.000 x 4,19 4,19
Rabat -0,70
3,49
CiasFrancuskie300g c 1.000 x 4,19 4,19
Rabat -0,70
3,49
MastExtraMlD01200g c 3.000 x 4,99 14,97
Rabat -6,00
8,97
ChlebPastReczZaw50( c 1.000 x 4,99 4,99
Nap Schwepp 1,21 A 1.000 x 6,39 6,39
But Plastik kaucja brak 1.000 x 0,50 0,50
MataAntyposDiament A 1.000 x 9,99 9,99
MataDoOdsMikFiCzar A 1.000 x 9,99 9,99
Sprzeda± opodatkowana C 172,52
PTU C 8,22
Sprzeda± opodatkowana brak 3,50
PTU brak 00/0 o,oo
Sprzeda± opodatkowana A 45,27
PTU A 23%
Sprzeda± opodatkowana B 27,05
PTU B 2,00
Suma PTU 18,69
Suma PLN 248
,
Kasa 13 Kasjer
Karta ptatnicza 248,34
Numer transakcji 4667
6360/13/4667/10.10.2026 Strona 2 z 2";

        [Theory]
        [InlineData("SokJabtkRivPet11 c 6.000 x 2,99 17,94")]
        [InlineData("But Plastik kaucja brak 6.000 x 0,50 3,00")]
        [InlineData("c 0.425 x 12,99 5,52")]                       // the name is on the line above
        [InlineData("Nap Schwepp 1,21 A 1.000 x 6,39 6,39")]       // numbers in the name
        [InlineData("  Mleko  B  2 x 3,00  6,00  ")]               // whole quantity, spaces around
        [InlineData("Mleko B 2,000 × 3,00 6,00")]                   // × and X are what OCR makes of the x
        [InlineData("Mleko B 2,000 X 3,00 6,00")]
        [InlineData("Zwrot kaucji brak 1.000 x 0,50 -0,50")]       // a return
        public void IsItemRow_ReceiptRow_True(string line)
        {
            Assert.True(ReceiptRows.IsItemRow(line));
        }

        [Theory]
        [InlineData("Rabat -3,60")]
        [InlineData("14,34")]
        [InlineData("Sprzeda± opodatkowana C 172,52")]
        [InlineData("Karta ptatnicza 248,34")]
        [InlineData("Suma PLN 248")]
        [InlineData("6360/13/4667/10.10.2026 Strona 1 z 2")]
        [InlineData("Nazwa PTU 110\" Cena Wartoéé")]
        [InlineData("Beer Baltyka 0 50,0 A")]                        // the other kind of receipt: a marked amount
        [InlineData("25.50 A")]
        [InlineData("")]
        public void IsItemRow_OtherLine_False(string line)
        {
            Assert.False(ReceiptRows.IsItemRow(line));
        }

        [Theory]
        [InlineData("Rabat -3,60", true)]
        [InlineData("  rabat -0,70 ", true)]
        [InlineData("Rabat 3,60", true)]
        [InlineData("Rabat", false)]
        [InlineData("14,34", false)]
        public void IsDiscountRow_Line(string line, bool expected)
        {
            Assert.Equal(expected, ReceiptRows.IsDiscountRow(line));
        }

        [Fact]
        public void Parse_ItemWithDiscount_NetAmountCountedOnce()
        {
            var items = ReceiptRows.Parse(new[]
            {
                "SokJabtkRivPet11 c 6.000 x 2,99 17,94",
                "Rabat -3,60",
                "14,34",
            });

            Assert.Equal(new[] { new ReceiptItem("SokJabtkRivPet11", 14.34) }, items);
        }

        [Fact]
        public void Parse_DiscountWithoutMinus_StillTakenOff()
        {
            var items = ReceiptRows.Parse(new[] { "Mleko B 1.000 x 5,49 5,49", "Rabat 1,20" });

            Assert.Equal(4.29, Assert.Single(items).Amount);
        }

        [Fact]
        public void Parse_NameOnTheLineAbove_UsedAsTheName()
        {
            var items = ReceiptRows.Parse(new[] { "papryKa 5toaKa", "c 0.425 x 12,99 5,52", "C.7erw" });

            var item = Assert.Single(items);
            Assert.Equal("papryKa 5toaKa", item.Name);
            Assert.Equal(5.52, item.Amount);
        }

        [Fact]
        public void Parse_NameOnTheRowItself_NotReplacedByTheLineAbove()
        {
            var items = ReceiptRows.Parse(new[] { "Kasa 13", "Mleko B 1.000 x 5,49 5,49" });

            Assert.Equal("Mleko", Assert.Single(items).Name);
        }

        [Fact]
        public void Parse_DiscountNotRightAfterAnItem_Ignored()
        {
            var items = ReceiptRows.Parse(new[] { "Rabat -1,00", "Mleko B 1.000 x 5,49 5,49", "Suma PTU 18,69", "Rabat -2,00" });

            Assert.Equal(5.49, Assert.Single(items).Amount);
        }

        [Fact]
        public void Parse_ReturnedItem_NegativeAmount()
        {
            var items = ReceiptRows.Parse(new[] { "Zwrot kaucji brak 1.000 x 0,50 -0,50" });

            Assert.Equal(-0.5, Assert.Single(items).Amount);
        }

        [Fact]
        public void Parse_WholeReceipt_ItemsCostWhatTheReceiptSays()
        {
            var items = ReceiptRows.Parse(RecipiesHelper.SplitLines(BiedronkaReceipt));

            Assert.Equal(32, items.Count);
            Assert.Equal(248.34, Math.Round(items.Sum(item => item.Amount), 2));
        }

        [Fact]
        public void CalculateCurrentAmount_ReceiptTable_PartPerItemAndTotalOfTheReceipt()
        {
            DbManual.SetupTests(new List<Common.Model.CategoryModel>());
            var vm = new Page1VM(-248.34) { Text = BiedronkaReceipt };

            vm.CalculateCurrentAmount();

            Assert.Equal(32, vm.Amounts.Count);
            Assert.Equal(-248.34, vm.CalculatedAmount);
            Assert.Equal(0.0, vm.Diff);
            Assert.All(vm.Amounts, part => Assert.True(part.FromAmount < 0)); // money spent
            Assert.Equal(-1434, vm.Amounts[0].FromAmount);                    // 17,94 - 3,60
            Assert.Equal("SokJabtkRivPet11", vm.Amounts[0].Note);
            Assert.Equal(Enumerable.Range(1, 32), vm.Amounts.Select(part => part.Order));
        }

        [Fact]
        public void CalculateCurrentAmount_ReceiptTable_CategoryFoundByWordOfTheName()
        {
            DbManual.SetupTests(new List<Common.Model.CategoryModel>
            {
                new Common.Model.CategoryModel { Id = 7, Title = "Dairy" },
                new Common.Model.CategoryModel { Id = 8, Title = "Mleko" },
            });
            var vm = new Page1VM(-5.49) { Text = "Mleko UHT 1l B 1.000 x 5,49 5,49" };

            vm.CalculateCurrentAmount();

            Assert.Equal(8, Assert.Single(vm.Amounts).CategoryId);
        }

        [Fact]
        public void Highlight_ReceiptTable_TextKeptAndAmountCounted()
        {
            DbManual.SetupTests(new List<Common.Model.CategoryModel>());
            var vm = new Page1VM(-248.34) { Text = BiedronkaReceipt };

            vm.HighlightCommand.Execute();

            Assert.Equal(BiedronkaReceipt, vm.Text);
            Assert.Equal(-248.34, vm.CalculatedAmount, 2);
        }

        [Fact]
        public void FormatText_ReceiptTable_Unchanged()
        {
            Assert.Equal(BiedronkaReceipt, RecipiesHelper.FormatText(BiedronkaReceipt));
        }

        [Fact]
        public void CalculateCurrentAmount_MarkedAmountsStillWork()
        {
            DbManual.SetupTests(new List<Common.Model.CategoryModel>());
            var vm = new Page1VM(-75.0) { Text = "Beer Baltyka 0 50,0 A\r\nMilk 25.0 A" };

            vm.CalculateCurrentAmount();

            Assert.Equal(2, vm.Amounts.Count);
            Assert.Equal(-75.0, vm.CalculatedAmount);
        }

        [Fact]
        public void RecipesVM_ReceiptTable_PartsReachTheSecondPage()
        {
            DbManual.SetupTests(new List<Common.Model.AccountFilterModel>());
            DbManual.SetupTests(new List<Common.Model.CurrencyModel>());
            DbManual.SetupTests(new List<Common.Model.CategoryModel>());
            var vm = new RecipesVM(-248.34);
            var page1 = (Page1VM)vm.CurrentPage;
            page1.Text = BiedronkaReceipt;

            vm.MoveNextCommand.Execute();

            var page2 = Assert.IsType<Page2VM>(vm.CurrentPage);
            Assert.Equal(32, page2.FinancistoTransactions.Count);
            page2.TotalCommand.Execute();
            Assert.Equal(-248.34, page2.CalculatedAmount, 2);
            Assert.Equal(0.0, page2.Diff, 2);
        }

        [Fact]
        public void BuildInlines_ReceiptTable_RowsAndDiscountsYellowAsAWhole()
        {
            var inlines = RecipesFormatter.BuildInlines("Header\r\nMleko B 1.000 x 5,49 5,49\r\nRabat -1,20\r\n4,29");

            var runs = inlines.OfType<Run>().ToList();
            var row = runs.Single(run => run.Text == "Mleko B 1.000 x 5,49 5,49");
            var discount = runs.Single(run => run.Text == "Rabat -1,20");
            Assert.Equal(Brushes.Yellow, row.Background);
            Assert.Equal(Brushes.Yellow, discount.Background);
            // the net amount only repeats the item: an ordinary number, green
            Assert.Equal(Brushes.LightGreen, runs.Single(run => run.Text == "4,29").Background);
            Assert.Equal(3, inlines.OfType<LineBreak>().Count());
        }
    }
}
