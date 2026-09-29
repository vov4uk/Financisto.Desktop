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
using Financisto.Desktop.Views.Dialogs;

namespace Financisto.Desktop.ViewModels.Pages
{
    /// <summary>
    /// Import rules (<see cref="DbManual.Rules"/>, persisted in rules.json, not in the backup).
    /// Every change is saved right away; a refresh re-reads the file.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class RulesPageVM : EntityBaseVM<RuleModel>
    {
        public RulesPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        protected override Task OnAdd() => OpenRulesDialogAsync(0);

        protected override Task OnDelete(RuleModel item) => OnRuleDelete(item.Id ?? 0);

        protected override Task OnEdit(RuleModel item) => OpenRulesDialogAsync(item.Id ?? 0);

        protected override async Task RefreshData()
        {
            await DbManual.LoadRulesAsync();
            var rules = DbManual.Rules.OrderBy(r => r.Created).ToList();
            foreach (var item in rules)
            {
                item.UpdateTitles();
            }
            Entities = new ObservableCollection<RuleModel>(rules);
        }

        private async Task OnRuleDelete(int id)
        {
            DbManual.Rules.Remove(DbManual.Rules.FirstOrDefault(r => r.Id == id));
            await DbManual.SaveRulesAsync();
            await RefreshData();
        }

        private async Task OpenRulesDialogAsync(int id)
        {
            RuleDto rule;

            if (id != 0)
            {
                var ruleModel = DbManual.Rules.FirstOrDefault(r => r.Id == id);
                if (ruleModel == null)
                {
                    return;
                }

                rule = new RuleDto(ruleModel);
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

            RuleDialogVM ruleVm = new RuleDialogVM(rule);

            var result = await dialogWrapper.ShowDialogAsync<RuleDialog>(ruleVm, 420, 440, LocalizationService.Instance.rule);

            var updatedItem = result as RuleDto;
            if (updatedItem != null)
            {
                int newId;
                if (id == 0)
                {
                    newId = (DbManual.Rules.Max(r => r.Id) ?? 0) + 1;
                }
                else
                {
                    var existingRule = DbManual.Rules.FirstOrDefault(r => r.Id == id);
                    DbManual.Rules.Remove(existingRule);
                    newId = id;
                }
                DbManual.Rules.Add(new RuleModel
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

                await DbManual.SaveRulesAsync();
                await RefreshData();
            }
        }
    }
}
