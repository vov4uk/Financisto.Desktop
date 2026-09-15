using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.ViewModels.Pages;

namespace Financisto.Desktop.ViewModel
{
    [ExcludeFromCodeCoverage]
    public class RulesVM : EntityBaseVM<RuleModel>
    {
        public RulesVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        protected override Task OnAdd() => OpenRulesDialogAsync(0);

        protected override Task OnDelete(RuleModel item) => OnRuleDelete(item.Id ?? 0);

        protected override Task OnEdit(RuleModel item) => OpenRulesDialogAsync(item.Id ?? 0);

        protected override async Task RefreshData()
        {
            await DbManual.SaveRulesAsync();
            await DbManual.LoadRulesAsync();
            Entities = new ObservableCollection<RuleModel>(DbManual.Rules.OrderBy(r => r.Created));
            foreach (var item in Entities)
            {
                item.UpdateTitles();
            }
        }

        private async Task OnRuleDelete(int id)
        {
            DbManual.Rules.Remove(DbManual.Rules.FirstOrDefault(r => r.Id == id));
            await RefreshData();
        }

        private async Task OpenRulesDialogAsync(int id)
        {

            RuleDto rule = null;

            if (id != 0)
            {
                var ruleModel = DbManual.Rules.FirstOrDefault(r => r.Id == id);
                if (ruleModel != null)
                {
                    rule = new RuleDto
                    {
                        CategoryId = ruleModel.CategoryId,
                        LocationId = ruleModel.LocationId,
                        PayeeId = ruleModel.PayeeId,
                        ProjectId = ruleModel.ProjectId,
                        Description = ruleModel.Description,
                        Condition = ruleModel.Condition,
                        Created = ruleModel.Created,
                        IsActive = ruleModel.IsActive,
                        MCCCategory = ruleModel.MCCCategory
                    };
                }
                else
                {
                    return;
                }

            }
            else
            {
                rule = new RuleDto()
                {
                    Description = "Description here",
                    Condition = RuleConditionType.DescriptionContains,
                    Created = DateTime.Now,
                    IsActive = true
                };
            }

            RuleControlVM ruleVm = new RuleControlVM(rule);

            var result = await dialogWrapper.ShowDialogAsync<RuleControl>(ruleVm, 380, 400, LocalizationService.Instance.rule);

            var updatedItem = result as RuleDto;
            if (updatedItem != null)
            {
                int newId = 0;
                if (id == 0)
                {
                    newId = (DbManual.Rules?.Max(r => r.Id) ?? 0) + 1;
                }
                else
                {
                    var existingRule = DbManual.Rules.FirstOrDefault(r => r.Id == id);
                    DbManual.Rules?.Remove(existingRule);
                    newId = id;
                }
                DbManual.Rules?.Add(new RuleModel
                {
                    Description = updatedItem.Description,
                    CategoryId = updatedItem.CategoryId,
                    Condition = updatedItem.Condition,
                    Created = updatedItem.Created,
                    Id = newId,
                    IsActive = updatedItem.IsActive,
                    LocationId = updatedItem.LocationId,
                    PayeeId = updatedItem.PayeeId,
                    ProjectId = updatedItem.ProjectId,
                    MCCCategory = updatedItem.MCCCategory
                });

                await RefreshData();
            }
        }
    }
}
