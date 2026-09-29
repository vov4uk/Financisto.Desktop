namespace Financisto.Adapter.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Financisto.DataAccess.Data;
    using Financisto.Tests.Common;
    using Xunit;

    public class EntityExtensionsTests
    {
        [Fact]
        public void WriteBackupLines_TransformTransactionToString_StringEquals()
        {
            var expectedString = @"$ENTITY:transactions
_id:3
from_account_id:3
to_account_id:0
category_id:2
project_id:0
location_id:0
note:ECMC5431 01.12.17 17:17 покупка 550р TEREMOK SCHUKA Баланс: 49820.45р
from_amount:-55000
to_amount:0
datetime:1515338499910
accuracy:0
latitude:0
longitude:0
is_template:0
status:PN
is_ccard_payment:0
last_recurrence:1515338499910
payee_id:0
parent_id:0
updated_on:1515338499910
original_currency_id:0
original_from_amount:0
$$
";

            var columnsOrder = new Dictionary<string, List<string>>
            {
                ["transactions"] = PredefinedData.TransactionsColumnsOrder,
            };

            var columnData = BuildColumnData(columnsOrder);

            using var sw = new StringWriter();
            PredefinedData.Transaction.WriteBackupLines(sw, columnData);

            Assert.Equal(
                expectedString.ReplaceLineEndings("\n"),
                sw.ToString().ReplaceLineEndings("\n"));
        }

        [Fact]
        public void WriteBackupLines_AliasesWithLineBreaks_WrittenEscapedOnOneLine()
        {
            var payee = new Payee { Id = 1, Title = "Mom", Aliases = "Mama\r\nMatka\nMutter" };

            var text = Write(payee, ("payee", new[] { "_id", "title", "aliases" }));

            Assert.Equal("$ENTITY:payee\n_id:1\ntitle:Mom\naliases:Mama\\nMatka\\nMutter\n$$\n", text);
        }

        [Fact]
        public void WriteBackupLines_NoteWithLineBreaks_LineBreaksReplacedWithSpace()
        {
            var transaction = new Transaction { Id = 1, Note = "first\r\nsecond\nthird" };

            var text = Write(transaction, ("transactions", new[] { "_id", "note" }));

            Assert.Equal("$ENTITY:transactions\n_id:1\nnote:first second third\n$$\n", text);
        }

        [Fact]
        public void WriteBackupLines_UnicodeTitle_WrittenAsIs()
        {
            var category = new Category { Id = 1, Title = "Food🥗" };

            var text = Write(category, ("category", new[] { "_id", "title" }));

            Assert.Equal("$ENTITY:category\n_id:1\ntitle:Food🥗\n$$\n", text);
        }

        [Fact]
        public void WriteBackupLines_SortOrderColumn_WrittenForAccountOnly()
        {
            var account = new Account { Id = 1, SortOrder = 5 };
            var tag = new Tag { Id = 1, Title = "Tag1", SortOrder = 3 };

            var accountText = Write(account, ("account", new[] { "_id", "sort_order" }));
            var tagText = Write(tag, ("tag", new[] { "_id", "title", "sort_order" }));

            Assert.Contains("sort_order:5", accountText);
            Assert.DoesNotContain("sort_order", tagText);
        }

        [Fact]
        public void WriteBackupLines_TableAbsentFromSourceBackup_WritesAllColumns()
        {
            // E.g. the first tag created in the app when the opened backup had no tag table.
            var tag = new Tag { Id = 9, Title = "New" };

            var text = Write(tag);

            Assert.Contains("_id:9", text);
            Assert.Contains("title:New", text);
            Assert.Contains("is_active:1", text);
        }

        [Fact]
        public void InBackupOrder_AccountsWithSortOrder_OrderedBySortOrderAndAppCreatedLast()
        {
            Entity[] accounts =
            [
                new Account { Id = 10, SortOrder = 0 },
                new Account { Id = 20, SortOrder = 2 },
                new Account { Id = 30, SortOrder = 1 },
            ];

            var ordered = accounts.InBackupOrder(typeof(Account)).Cast<Account>().Select(x => x.Id);

            Assert.Equal(new[] { 30, 20, 10 }, ordered);
        }

        [Fact]
        public void InBackupOrder_TypeWithoutSortOrder_KeepsSourceOrder()
        {
            Entity[] transactions =
            [
                new Transaction { Id = 3 },
                new Transaction { Id = 1 },
                new Transaction { Id = 2 },
            ];

            var ordered = transactions.InBackupOrder(typeof(Transaction)).Cast<Transaction>().Select(x => x.Id);

            Assert.Equal(new[] { 3, 1, 2 }, ordered);
        }

        private static string Write(Entity entity, params (string Table, string[] Columns)[] tables)
        {
            var columnsOrder = tables.ToDictionary(x => x.Table, x => x.Columns.ToList());

            using var sw = new StringWriter();
            entity.WriteBackupLines(sw, BuildColumnData(columnsOrder));
            return sw.ToString().ReplaceLineEndings("\n");
        }

        private static Dictionary<string, (Dictionary<string, int> Index, int Count)> BuildColumnData(
            Dictionary<string, List<string>> columnsOrder)
        {
            var result = new Dictionary<string, (Dictionary<string, int>, int)>(columnsOrder.Count);
            foreach (var (table, cols) in columnsOrder)
            {
                var index = new Dictionary<string, int>(cols.Count);
                for (int i = 0; i < cols.Count; i++)
                {
                    index[cols[i]] = i;
                }

                result[table] = (index, cols.Count);
            }

            return result;
        }
    }
}
