namespace Financisto.Desktop.Tests.Rules
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Financisto.Common;
    using Financisto.Common.Entities;
    using Financisto.Common.Model;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Helpers;
    using Moq;
    using Xunit;

    /// <summary>Storing the rules the import wizard added to <see cref="DbManual.Rules"/>.</summary>
    [Collection("Integration tests")]
    public sealed class RuleCreatorTest : IDisposable
    {
        public RuleCreatorTest()
        {
            DbManual.ResetAllDatabaseManuals();
        }

        public void Dispose()
        {
            DbManual.ResetAllDatabaseManuals();
        }

        [Fact]
        public async Task SaveNewAsync_RuleWithoutId_StoredAndReloadedWithItsId()
        {
            using var db = new FinancistoDatabaseFactory().CreateDatabase();
            await db.ImportEntitiesAsync(Array.Empty<Entity>()); // creates the default rows the rule row points to
            var rule = new RuleModel { IsActive = true, Condition = RuleConditionType.DescriptionContains, Description = "mleko", CategoryId = 0 };
            DbManual.SetupTests(new List<RuleModel> { rule });

            await RuleCreator.SaveNewAsync(db);

            var stored = Assert.Single(await new RulesRepository(db).LoadAsync());
            Assert.Equal("mleko", stored.Description);
            Assert.True(Assert.Single(DbManual.Rules).Id > 0);
        }

        [Fact]
        public async Task SaveNewAsync_NoNewRule_DatabaseNotTouched()
        {
            var db = new Mock<IFinancistoDatabase>(MockBehavior.Strict);
            DbManual.SetupTests(new List<RuleModel> { new RuleModel { Id = 3, IsActive = true, Description = "old", CategoryId = 1 } });

            await RuleCreator.SaveNewAsync(db.Object);

            Assert.Equal(3, Assert.Single(DbManual.Rules).Id);
        }
    }
}
