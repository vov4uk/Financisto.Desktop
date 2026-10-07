namespace Financisto.Desktop.Tests.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Xunit;

    /// <summary>Import rules are stored as sms_template rows, so they travel with the backup.</summary>
    [Collection("Integration tests")]
    public class RuleStorageTest
    {
        [Fact]
        public async Task SaveAndLoad_AllRuleFields_RoundTripThroughSmsTemplate()
        {
            using var db = await CreateDbAsync();
            var repository = new RulesRepository(db);

            await repository.SaveAsync(new List<RuleModel>
            {
                new RuleModel { Condition = RuleConditionType.DescriptionContains, Description = "lidl", CategoryId = 5, PayeeId = 6, ProjectId = 7, LocationId = 8, IsActive = true, Created = new DateTime(2026, 10, 1, 12, 0, 0) },
                new RuleModel { Condition = RuleConditionType.DescriptionMatches, Description = "a: b", IsActive = false },
                new RuleModel { Condition = RuleConditionType.MCC, MCCCategory = Mcc.none, CategoryId = 9, IsActive = true },
            });
            var rules = await repository.LoadAsync();

            Assert.Equal(3, rules.Count);
            Assert.All(rules, r => Assert.True(r.Id > 0));

            Assert.Equal(RuleConditionType.DescriptionContains, rules[0].Condition);
            Assert.Equal("lidl", rules[0].Description);
            Assert.Equal((5, 6, 7, 8), (rules[0].CategoryId, rules[0].PayeeId, rules[0].ProjectId, rules[0].LocationId));
            Assert.Equal(new DateTime(2026, 10, 1, 12, 0, 0), rules[0].Created);
            Assert.True(rules[0].IsActive);

            Assert.Equal(RuleConditionType.DescriptionMatches, rules[1].Condition);
            Assert.Equal("a: b", rules[1].Description);
            Assert.False(rules[1].IsActive);
            Assert.Null(rules[1].CategoryId);
            Assert.Null(rules[1].LocationId);

            Assert.Equal(RuleConditionType.MCC, rules[2].Condition);
            Assert.Equal(Mcc.none, rules[2].MCCCategory);
            Assert.Equal(9, rules[2].CategoryId);
        }

        [Fact]
        public async Task Save_RuleRows_AreNotMatchableByAndroid_AndRealTemplatesSurvive()
        {
            using var db = await CreateDbAsync(new SmsTemplate { Id = 1, Title = "Bank", Template = "Paid {{a}}", CategoryId = 2 });

            await new RulesRepository(db).SaveAsync(new List<RuleModel>
            {
                new RuleModel { Condition = RuleConditionType.DescriptionContains, Description = "lidl", LocationId = 4, IsActive = true },
            });

            using var uow = db.CreateUnitOfWork();
            var rows = (await uow.GetRepository<SmsTemplate>().GetAllAsync()).OrderBy(t => t.Id).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal("Paid {{a}}", rows[0].Template);
            Assert.Equal("contains:lidl", rows[1].Description);
            Assert.Equal(RuleSmsTemplateMapper.Marker, rows[1].Template);
            Assert.Equal(4, rows[1].LocationId);
        }

        [Fact]
        public async Task Save_RuleRemoved_DeletesItsRowOnly()
        {
            using var db = await CreateDbAsync();
            var repository = new RulesRepository(db);
            await repository.SaveAsync(new List<RuleModel>
            {
                new RuleModel { Description = "a", IsActive = true },
                new RuleModel { Description = "b", IsActive = true },
            });

            var rules = await repository.LoadAsync();
            rules.RemoveAt(0);
            await repository.SaveAsync(rules);

            Assert.Equal("b", Assert.Single(await repository.LoadAsync()).Description);
        }

        [Fact]
        public async Task Setup_DatabaseHasRules_LoadsThemIntoDbManual()
        {
            using var db = await CreateDbAsync();
            await new RulesRepository(db).SaveAsync(new List<RuleModel> { new RuleModel { Description = "lidl", IsActive = true } });

            await DbManual.SetupAsync(db);

            Assert.Equal("lidl", Assert.Single(DbManual.Rules).Description);
            DbManual.ResetAllDatabaseManuals();
        }

        [Theory]
        [InlineData("contains:lidl", RuleConditionType.DescriptionContains, "lidl")]
        [InlineData("MATCHES:Lidl Kyiv", RuleConditionType.DescriptionMatches, "Lidl Kyiv")]
        [InlineData("no prefix", RuleConditionType.DescriptionContains, "no prefix")]
        public void DecodeCondition_Description_ParsesPrefix(string description, RuleConditionType condition, string text)
        {
            var rule = RuleSmsTemplateMapper.ToRule(new SmsTemplate { Id = 1, Description = description, Template = RuleSmsTemplateMapper.Marker });

            Assert.Equal(condition, rule.Condition);
            Assert.Equal(text, rule.Description);
        }

        private static async Task<IFinancistoDatabase> CreateDbAsync(params Entity[] entities)
        {
            DbManual.ResetAllDatabaseManuals();
            var db = new FinancistoDatabaseFactory().CreateDatabase();
            await db.ImportEntitiesAsync(entities);
            return db;
        }
    }
}
