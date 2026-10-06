namespace Financisto.Desktop.Tests.Rules
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Newtonsoft.Json;
    using Xunit;

    /// <summary>Import rules are stored as sms_template rows, so they travel with the backup.</summary>
    [Collection("Integration tests")]
    public class RuleStorageTest
    {
        [Fact]
        public async Task SaveAndLoad_AllRuleFields_RoundTripThroughSmsTemplate()
        {
            using var db = await CreateDbAsync();
            DbManual.SetupTests(new List<RuleModel>
            {
                new RuleModel { Id = 1, Condition = RuleConditionType.DescriptionContains, Description = "lidl", CategoryId = 5, PayeeId = 6, ProjectId = 7, LocationId = 8, IsActive = true, Created = new DateTime(2026, 10, 1, 12, 0, 0) },
                new RuleModel { Id = 2, Condition = RuleConditionType.DescriptionMatches, Description = "a: b", IsActive = false },
                new RuleModel { Id = 3, Condition = RuleConditionType.MCC, MCCCategory = Mcc.none, CategoryId = 9, IsActive = true },
            });
            await DbManual.SetupAsync(db);

            await DbManual.SaveRulesAsync();
            DbManual.SetupTests(new List<RuleModel>());
            await DbManual.LoadRulesAsync();

            var rules = DbManual.Rules;
            Assert.Equal(3, rules.Count);
            Assert.Equal(new[] { 1, 2, 3 }, rules.Select(r => r.Id!.Value));

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

            DbManual.ResetAllDatabaseManuals();
        }

        [Fact]
        public async Task Save_RuleRows_AreNotMatchableByAndroid_AndRealTemplatesSurvive()
        {
            using var db = await CreateDbAsync(new SmsTemplate { Id = 1, Title = "Bank", Template = "Paid {{a}}", CategoryId = 2 });
            DbManual.SetupTests(new List<RuleModel>
            {
                new RuleModel { Id = 1, Condition = RuleConditionType.DescriptionContains, Description = "lidl", LocationId = 4, IsActive = true },
            });
            await DbManual.SetupAsync(db);

            await DbManual.SaveRulesAsync();

            using var uow = db.CreateUnitOfWork();
            var rows = (await uow.GetRepository<SmsTemplate>().GetAllAsync()).OrderBy(t => t.Id).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal("Paid {{a}}", rows[0].Template);
            Assert.Equal("contains:lidl", rows[1].Description);
            Assert.Equal(RuleSmsTemplateMapper.Marker, rows[1].Template);
            Assert.Equal(4, rows[1].LocationId);
            Assert.NotEqual(1, rows[1].Id);
            Assert.Equal(rows[1].Id, DbManual.Rules[0].Id);

            DbManual.SetupTests(new List<RuleModel>());
            await DbManual.LoadRulesAsync();
            Assert.Single(DbManual.Rules);

            DbManual.ResetAllDatabaseManuals();
        }

        [Fact]
        public async Task Save_RuleRemoved_DeletesItsRowOnly()
        {
            using var db = await CreateDbAsync();
            DbManual.SetupTests(new List<RuleModel>
            {
                new RuleModel { Id = 1, Description = "a", IsActive = true },
                new RuleModel { Id = 2, Description = "b", IsActive = true },
            });
            await DbManual.SetupAsync(db);
            await DbManual.SaveRulesAsync();

            DbManual.Rules.RemoveAt(0);
            await DbManual.SaveRulesAsync();
            await DbManual.LoadRulesAsync();

            Assert.Equal("b", Assert.Single(DbManual.Rules).Description);
            DbManual.ResetAllDatabaseManuals();
        }

        [Fact]
        public async Task Load_NoRulesInBackup_ImportsLegacyRulesJsonOnce()
        {
            using var db = await CreateDbAsync();
            var path = Path.Combine(Path.GetTempPath(), "Financisto.Desktop.Tests", Guid.NewGuid().ToString("N"), "rules.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var original = DbManual.RulesPath;
            DbManual.RulesPath = path;
            try
            {
                await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(new List<RuleModel>
                {
                    new RuleModel { Id = 1, Description = "legacy", IsActive = true },
                }));
                DbManual.SetupTests(new List<RuleModel>());
                await DbManual.SetupAsync(db);

                await DbManual.LoadRulesAsync();

                Assert.Equal("legacy", Assert.Single(DbManual.Rules).Description);
                Assert.False(File.Exists(path));
                DbManual.SetupTests(new List<RuleModel>());
                await DbManual.LoadRulesAsync();
                Assert.Equal("legacy", Assert.Single(DbManual.Rules).Description);
            }
            finally
            {
                DbManual.RulesPath = original;
                DbManual.ResetAllDatabaseManuals();
            }
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
