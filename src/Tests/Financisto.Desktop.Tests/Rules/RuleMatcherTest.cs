namespace Financisto.Desktop.Tests.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.Wizards;
    using Xunit;

    /// <summary>The import rules applied to a row of the import / recipes wizards.</summary>
    public sealed class RuleMatcherTest : IDisposable
    {
        public void Dispose()
        {
            DbManual.ResetAllDatabaseManuals();
        }

        [Theory]
        [InlineData("Payment to Amazon Store", "amazon")]
        [InlineData("AMAZON", "Amazon")]
        [InlineData("Amazon", "Amazon")]
        public void Apply_DescriptionContains_NoteHasTheText_RuleApplied(string note, string description)
        {
            var transaction = new FinancistoTransactionDto { Note = note };

            RuleMatcher.Apply(transaction, new[] { Contains(description, categoryId: 5) });

            Assert.Equal(5, transaction.CategoryId);
        }

        [Fact]
        public void Apply_NoteDoesNotHaveTheText_Unchanged()
        {
            var transaction = new FinancistoTransactionDto { Note = "Lidl", CategoryId = 1 };

            RuleMatcher.Apply(transaction, new[] { Contains("amazon", categoryId: 5) });

            Assert.Equal(1, transaction.CategoryId);
        }

        [Fact]
        public void Apply_AllFourFieldsOfTheRule_Filled()
        {
            var transaction = new FinancistoTransactionDto { Note = "lidl" };
            var rule = Contains("lidl", categoryId: 5);
            rule.LocationId = 6;
            rule.PayeeId = 7;
            rule.ProjectId = 8;

            RuleMatcher.Apply(transaction, new[] { rule });

            Assert.Equal((5, 6, 7, 8), (transaction.CategoryId, transaction.LocationId, transaction.PayeeId, transaction.ProjectId));
        }

        [Fact]
        public void Apply_RuleWithoutAField_LeavesTheFieldAlone()
        {
            var transaction = new FinancistoTransactionDto { Note = "lidl", CategoryId = 1, ProjectId = 2 };

            RuleMatcher.Apply(transaction, new[] { Contains("lidl", locationId: 6) });

            Assert.Equal((1, 6, 2), (transaction.CategoryId, transaction.LocationId, transaction.ProjectId));
        }

        [Fact]
        public void Apply_DescriptionMatches_WholeTextOnly()
        {
            var rule = new RuleModel { IsActive = true, Condition = RuleConditionType.DescriptionMatches, Description = "Lidl", CategoryId = 5 };
            var same = new FinancistoTransactionDto { Note = "LIDL" };
            var longer = new FinancistoTransactionDto { Note = "Lidl Kyiv" };

            RuleMatcher.Apply(same, new[] { rule });
            RuleMatcher.Apply(longer, new[] { rule });

            Assert.Equal(5, same.CategoryId);
            Assert.Equal(0, longer.CategoryId);
        }

        [Fact]
        public void Apply_InactiveRule_Ignored()
        {
            var transaction = new FinancistoTransactionDto { Note = "lidl" };
            var rule = Contains("lidl", categoryId: 5);
            rule.IsActive = false;

            RuleMatcher.Apply(transaction, new[] { rule });

            Assert.Equal(0, transaction.CategoryId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Apply_RuleWithoutText_MatchesNothing(string description)
        {
            // an empty "contains" would match every row
            var transaction = new FinancistoTransactionDto { Note = "lidl" };
            var rules = new[]
            {
                Contains(description, categoryId: 5),
                new RuleModel { IsActive = true, Condition = RuleConditionType.DescriptionMatches, Description = description, CategoryId = 6 },
            };

            RuleMatcher.Apply(transaction, rules);

            Assert.Equal(0, transaction.CategoryId);
        }

        [Fact]
        public void Apply_RowWithoutNote_NothingToMatch()
        {
            var transaction = new FinancistoTransactionDto { Note = null };

            RuleMatcher.Apply(transaction, new[] { Contains("lidl", categoryId: 5) });

            Assert.Equal(0, transaction.CategoryId);
        }

        [Fact]
        public void Apply_SeveralRulesMatch_TheLastOneWins()
        {
            var transaction = new FinancistoTransactionDto { Note = "Lidl Kyiv" };

            RuleMatcher.Apply(transaction, new[] { Contains("lidl", categoryId: 5, locationId: 1), Contains("kyiv", categoryId: 6) });

            Assert.Equal((6, 1), (transaction.CategoryId, transaction.LocationId));
        }

        [Fact]
        public void Apply_NoRulesGiven_UsesTheImportRules()
        {
            DbManual.SetupTests(new List<RuleModel> { Contains("lidl", categoryId: 5) });
            var transaction = new FinancistoTransactionDto { Note = "Lidl" };

            RuleMatcher.Apply(transaction);

            Assert.Equal(5, transaction.CategoryId);
        }

        [Fact]
        public void Apply_MccRule_MatchesTheCodeOfTheRowOnly()
        {
            var (mcc, codes) = DbManual.MCCEnums.First(pair => pair.Value.Any(code => code > 0));
            var rule = new RuleModel { IsActive = true, Condition = RuleConditionType.MCC, MCCCategory = mcc, CategoryId = 5 };
            var withCode = new FinancistoTransactionDto { MCC = codes.First(code => code > 0) };
            var noCode = new FinancistoTransactionDto { MCC = 0 };

            RuleMatcher.Apply(withCode, new[] { rule });
            RuleMatcher.Apply(noCode, new[] { rule });

            Assert.Equal(5, withCode.CategoryId);
            Assert.Equal(0, noCode.CategoryId);
        }

        private static RuleModel Contains(string description, int? categoryId = null, int? locationId = null) => new RuleModel
        {
            IsActive = true,
            Condition = RuleConditionType.DescriptionContains,
            Description = description,
            CategoryId = categoryId,
            LocationId = locationId,
        };
    }
}
