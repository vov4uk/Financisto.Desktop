namespace Financisto.Desktop.Tests.Rules
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Financisto.Common.Entities;
    using Financisto.Common.Localization;
    using Financisto.Common.Model;
    using Financisto.DataAccess;
    using Financisto.DataAccess.Abstractions;
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.Helpers;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Financisto.Desktop.ViewModels.Pages;
    using Financisto.Desktop.Views.Dialogs;
    using Moq;
    using Xunit;

    /// <summary>Templates and rules of the page are both rows of sms_template; each dialog saves its row to the database.</summary>
    [Collection("Integration tests")]
    public class SmsTemplatesPageVMTest
    {
        private readonly Mock<IDialogWrapper> dialogMock = new();

        [Fact]
        public async Task Add_TemplateDialogSaved_StoresAndroidCompatibleRow()
        {
            using var db = await CreateDbAsync();
            dialogMock
                .Setup(d => d.ShowDialogAsync<SmsTemplateDialog>(It.IsAny<DialogBaseVM>(), 760, 560, LocalizationService.Instance.sms_template))
                .ReturnsAsync(new SmsTemplateDto
                {
                    Sender = "Bank",
                    Description = "Card purchases",
                    Template = "Paid {{p}} at {{e}}.",
                    Note = "{{t}}",
                    IsIncome = false,
                    MatchGroupSummary = true,
                    CategoryId = 4,
                    PayeeId = 5,
                    ProjectId = 6,
                    LocationId = 7,
                    AccountId = null,
                    ToAccountId = null,
                });
            var vm = new SmsTemplatesPageVM(db, dialogMock.Object);

            await vm.AddCommand.ExecuteAsync();

            using var uow = db.CreateUnitOfWork();
            var row = Assert.Single(await uow.GetRepository<SmsTemplate>().GetAllAsync());
            Assert.True(row.Id > 0);
            Assert.Equal(("Bank", "Card purchases", "Paid {{p}} at {{e}}."), (row.Title, row.Description, row.Template));
            Assert.Equal((4, 5, 6, 7), (row.CategoryId, row.PayeeId, row.ProjectId, row.LocationId));
            Assert.True(row.MatchGroupSummary);
            Assert.Equal((-1, -1), (row.AccountId, row.ToAccountId));

            var item = Assert.Single(vm.Entities);
            Assert.False(item.IsRule);
            Assert.Equal("Bank", item.Title);
            DbManual.ResetAllDatabaseManuals();
        }

        [Fact]
        public async Task Add_RuleDialogSaved_StoresRuleRowNextToTemplates()
        {
            using var db = await CreateDbAsync(new SmsTemplate { Id = 1, Title = "Bank", Template = "Paid {{p}}", CategoryId = 2 });
            dialogMock
                .Setup(d => d.ShowDialogAsync<RuleDialog>(It.IsAny<DialogBaseVM>(), 420, 440, LocalizationService.Instance.rule))
                .ReturnsAsync(new RuleDto
                {
                    Description = "lidl",
                    Condition = RuleConditionType.DescriptionContains,
                    IsActive = true,
                    CategoryId = 3,
                    LocationId = 9,
                });
            var vm = new SmsTemplatesPageVM(db, dialogMock.Object);

            await vm.AddRuleCommand.ExecuteAsync();

            using var uow = db.CreateUnitOfWork();
            var rows = (await uow.GetRepository<SmsTemplate>().GetAllAsync()).OrderBy(r => r.Id).ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal("contains:lidl", rows[1].Description);
            Assert.Equal(9, rows[1].LocationId);
            Assert.Equal(new[] { false, true }, vm.Entities.Select(e => e.IsRule));
            DbManual.ResetAllDatabaseManuals();
        }

        [Fact]
        public async Task Edit_TemplateAndRule_OpenTheirOwnDialog()
        {
            using var db = await CreateDbAsync(new SmsTemplate { Id = 1, Title = "Bank", Template = "Paid {{p}}", CategoryId = 2 });
            DbManual.SetupTests(new List<RuleModel> { new RuleModel { Id = 2, Description = "lidl", IsActive = true, CategoryId = 3 } });
            await DbManual.SetupAsync(db);
            await DbManual.SaveRulesAsync();
            var vm = new SmsTemplatesPageVM(db, dialogMock.Object);
            await vm.RefreshDataCommand.ExecuteAsync();

            vm.SelectedValue = vm.Entities.Single(e => !e.IsRule);
            await vm.EditCommand.ExecuteAsync();
            dialogMock.Verify(d => d.ShowDialogAsync<SmsTemplateDialog>(It.IsAny<DialogBaseVM>(), 760, 560, It.IsAny<string>()), Times.Once);
            dialogMock.Verify(d => d.ShowDialogAsync<RuleDialog>(It.IsAny<DialogBaseVM>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>()), Times.Never);

            vm.SelectedValue = vm.Entities.Single(e => e.IsRule);
            await vm.EditCommand.ExecuteAsync();
            dialogMock.Verify(d => d.ShowDialogAsync<RuleDialog>(It.IsAny<DialogBaseVM>(), 420, 440, It.IsAny<string>()), Times.Once);
            DbManual.ResetAllDatabaseManuals();
        }

        [Fact]
        public async Task Delete_RuleAndTemplate_RemovesOnlyTheSelectedRow()
        {
            using var db = await CreateDbAsync(new SmsTemplate { Id = 1, Title = "Bank", Template = "Paid {{p}}", CategoryId = 2 });
            DbManual.SetupTests(new List<RuleModel> { new RuleModel { Id = 2, Description = "lidl", IsActive = true, CategoryId = 3 } });
            await DbManual.SetupAsync(db);
            await DbManual.SaveRulesAsync();
            var vm = new SmsTemplatesPageVM(db, dialogMock.Object);
            await vm.RefreshDataCommand.ExecuteAsync();
            Assert.Equal(2, vm.Entities.Count);

            vm.SelectedValue = vm.Entities.Single(e => e.IsRule);
            await vm.DeleteCommand.ExecuteAsync();

            Assert.Empty(DbManual.Rules);
            Assert.False(Assert.Single(vm.Entities).IsRule);

            vm.SelectedValue = vm.Entities.Single();
            await vm.DeleteCommand.ExecuteAsync();

            Assert.Empty(vm.Entities);
            DbManual.ResetAllDatabaseManuals();
        }

        [Fact]
        public void DialogVM_ExampleMatchesTemplate_ShowsParsedValues()
        {
            var vm = new SmsTemplateDialogVM(new SmsTemplateDto { Sender = "Bank", Template = "Paid {{p}} at {{e}}." });

            vm.Example = "Paid 12,50 at Lidl.";

            Assert.Contains("PRICE: 12.50", vm.ParseResult);
            Assert.Contains("PAYEE: Lidl", vm.ParseResult);
            Assert.Contains("ACCOUNT: " + LocalizationService.Instance.tpl_parse_not_found, vm.ParseResult);
        }

        [Fact]
        public void DialogVM_SenderOrTemplateEmpty_CannotSave()
        {
            var dto = new SmsTemplateDto { Sender = "Bank", Template = string.Empty };
            var vm = new SmsTemplateDialogVM(dto);
            Assert.False(vm.SaveCommand.CanExecute(null));

            dto.Template = "Paid {{p}}";
            Assert.True(vm.SaveCommand.CanExecute(null));

            dto.Sender = " ";
            Assert.False(vm.SaveCommand.CanExecute(null));
        }

        private static async Task<IFinancistoDatabase> CreateDbAsync(params Entity[] entities)
        {
            DbManual.ResetAllDatabaseManuals();
            DbManual.SetupTests(new List<RuleModel>());
            var db = new FinancistoDatabaseFactory().CreateDatabase();
            await db.ImportEntitiesAsync(entities);
            await DbManual.SetupAsync(db);
            return db;
        }
    }
}
