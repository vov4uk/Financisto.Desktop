namespace Financisto.Desktop.Tests.Wizards.Recipes
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using Financisto.Desktop.Helpers;
    using Xunit;

    /// <summary>
    /// The row patterns are quadratic on a long run of spaces (2000 of them took a second and then threw RegexMatchTimeoutException into the page):
    /// a line longer than any receipt row is not matched at all.
    /// </summary>
    public class ReceiptRowsLongLineTests
    {
        private const string RowTail = " 1 * 2.00 2.00 A"; // 16 characters

        [Theory]
        [InlineData(2000)]
        [InlineData(20000)]
        [InlineData(100000)]
        public void SpacesOnly_NoRow_AndNoWaiting(int spaces)
        {
            var lines = new[]
            {
                new string(' ', spaces),
                new string(' ', spaces) + "x",
                "x" + new string(' ', spaces),
                "a" + new string(' ', spaces) + "b",
                "a" + new string(' ', spaces) + "1 * 2.00 2.00 A",
                "a" + new string(' ', spaces) + "-1,00",
            };

            var stopwatch = Stopwatch.StartNew();
            foreach (var line in lines)
            {
                Assert.False(ReceiptRows.IsItemRow(line));
                Assert.False(ReceiptRows.IsNamelessItemRow(line));
                Assert.False(ReceiptRows.IsDiscountRow(line));
            }

            Assert.True(stopwatch.ElapsedMilliseconds < 500, $"{stopwatch.ElapsedMilliseconds} ms");
        }

        [Fact]
        public void RowOfExactlyTheLimit_IsARow_OneLonger_IsNot()
        {
            var atLimit = new string('a', 300 - RowTail.Length) + RowTail;
            var tooLong = new string('a', 301 - RowTail.Length) + RowTail;

            Assert.Equal(300, atLimit.Length);
            Assert.True(ReceiptRows.IsItemRow(atLimit));
            Assert.False(ReceiptRows.IsItemRow(tooLong));
        }

        [Fact]
        public void IndentationDoesNotCountTowardsTheLimit()
        {
            // receipts are padded to the right: the row itself is short
            Assert.True(ReceiptRows.IsItemRow(new string(' ', 5000) + "Mleko" + RowTail));
            Assert.True(ReceiptRows.IsItemRow("Mleko" + RowTail + new string(' ', 5000)));
        }

        [Fact]
        public void Parse_LongLinesAmongTheRows_Skipped_TheRestParsed()
        {
            var items = ReceiptRows.Parse(new[]
            {
                "Mleko",
                "1 * 2.00 2.00 A",
                new string(' ', 5000) + "junk",
                "a" + new string(' ', 5000) + "b",
                "Chleb",
                "2 * 3.00 6.00 A",
                new string('x', 100000),
            });

            Assert.Equal(new[] { new ReceiptItem("Mleko", 2.0), new ReceiptItem("Chleb", 6.0) }, items);
        }

        [Fact]
        public void PageAsOneLongLine_IsNoReceiptTable()
        {
            var page = string.Concat(Enumerable.Repeat("Mleko 1 * 2.00 2.00 A ", 5000));

            Assert.False(ReceiptRows.IsReceiptTable(new[] { page }));
        }

        [Fact]
        public void IsNameOfNextItemRow_LongLines_NoThrow()
        {
            var lines = new[] { new string('n', 5000), "1 * 2.00 2.00 A" };

            // a long line of text is still the name of the row below it, and finding that out must not throw
            Assert.True(ReceiptRows.IsNameOfNextItemRow(lines, 0));
        }
    }
}
