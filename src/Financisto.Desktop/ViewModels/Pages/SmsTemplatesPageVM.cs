using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.Converters;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.Views.Dialogs;

namespace Financisto.Desktop.ViewModels.Pages
{
    /// <summary>
    /// The sms_template table of the open backup: templates shared with the Android app, plus the desktop-only import rules
    /// (<see cref="RuleSmsTemplateMapper"/>). Every change is written to the database right away and reaches the file on
    /// Save backup; a refresh re-reads it.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class SmsTemplatesPageVM : EntityBaseVM<SmsTemplateModel>
    {
        private IAsyncCommand _addRuleCommand;

        public SmsTemplatesPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        public IAsyncCommand AddRuleCommand => _addRuleCommand ??= new AsyncCommand(() => OpenRuleDialogAsync(0));

        protected override Task OnAdd() => OpenTemplateDialogAsync(0);

        protected override Task OnEdit(SmsTemplateModel item)
            => item.IsRule ? OpenRuleDialogAsync(item.Id ?? 0) : OpenTemplateDialogAsync(item.Id ?? 0);

        protected override async Task OnDelete(SmsTemplateModel item)
        {
            int id = item.Id ?? 0;
            if (item.IsRule)
            {
                DbManual.Rules.RemoveAll(r => r.Id == id);
                await DbManual.SaveRulesAsync();
            }
            else
            {
                using (var uow = db.CreateUnitOfWork())
                {
                    var repo = uow.GetRepository<SmsTemplate>();
                    var template = await repo.FindByAsync(t => t.Id == id);
                    if (template != null)
                    {
                        await repo.DeleteAsync(template);
                        await uow.SaveChangesAsync();
                    }
                }
            }

            await RefreshData();
        }

        protected override async Task RefreshData()
        {
            await DbManual.LoadRulesAsync();

            List<SmsTemplate> rows;
            using (var uow = db.CreateUnitOfWork())
            {
                rows = await uow.GetRepository<SmsTemplate>().GetAllAsync();
            }

            var categories = Titles(DbManual.Category.Select(c => (c.Id, c.Title)));
            var payees = Titles(DbManual.Payee.Select(p => (p.Id, p.Title)));
            var projects = Titles(DbManual.Project.Select(p => (p.Id, p.Title)));
            var locations = Titles(DbManual.Location.Select(l => (l.Id, l.Title)));
            var accounts = Titles(DbManual.Account.Select(a => (a.Id, a.Title)));

            var items = rows
                .OrderBy(r => RuleSmsTemplateMapper.IsRule(r))
                .ThenBy(r => r.SortOrder)
                .ThenBy(r => r.Id)
                .Select(row => ToModel(row, categories, payees, projects, locations, accounts))
                .ToList();
            Entities = new ObservableCollection<SmsTemplateModel>(items);
        }

        private static Dictionary<int, string> Titles(IEnumerable<(int? Id, string Title)> items)
            => items.Where(i => i.Id > 0).GroupBy(i => i.Id!.Value).ToDictionary(g => g.Key, g => g.First().Title);

        private static string Lookup(Dictionary<int, string> titles, int id)
            => titles.TryGetValue(id, out var title) ? title : string.Empty;

        private static SmsTemplateModel ToModel(
            SmsTemplate row,
            Dictionary<int, string> categories,
            Dictionary<int, string> payees,
            Dictionary<int, string> projects,
            Dictionary<int, string> locations,
            Dictionary<int, string> accounts)
        {
            var model = new SmsTemplateModel
            {
                Id = row.Id,
                IsActive = row.IsActive,
                IsRule = RuleSmsTemplateMapper.IsRule(row),
                Category = Lookup(categories, row.CategoryId),
                Payee = Lookup(payees, row.PayeeId),
                Project = Lookup(projects, row.ProjectId),
                Location = Lookup(locations, row.LocationId),
                Account = Lookup(accounts, row.AccountId),
            };

            if (model.IsRule)
            {
                var rule = RuleSmsTemplateMapper.ToRule(row);
                rule.UpdateTitles();
                model.Kind = LocalizationService.Instance.sms_kind_rule;
                model.Title = rule.Title;
                model.Description = $"{rule.Condition.GetEnumDescription()}: {rule.UserFirendlyDescription}";
                model.Template = string.Empty;
                model.Account = string.Empty;
            }
            else
            {
                model.Kind = LocalizationService.Instance.sms_kind_template;
                model.Title = row.Title;
                model.Description = row.Description;
                model.Template = row.Template;
            }

            return model;
        }

        private async Task OpenTemplateDialogAsync(int id)
        {
            SmsTemplate entity;
            int nextSortOrder;
            using (var uow = db.CreateUnitOfWork())
            {
                var repo = uow.GetRepository<SmsTemplate>();
                nextSortOrder = (await repo.GetAllAsync()).Select(t => t.SortOrder).DefaultIfEmpty(0).Max() + 1;
                entity = id == 0 ? new SmsTemplate { Id = 0 } : await repo.FindByAsync(t => t.Id == id);
            }

            if (entity == null)
            {
                return;
            }

            var dto = id == 0 ? new SmsTemplateDto() : new SmsTemplateDto(entity);
            var dialogVm = new SmsTemplateDialogVM(dto);
            var result = await dialogWrapper.ShowDialogAsync<SmsTemplateDialog>(dialogVm, 760, 560, LocalizationService.Instance.sms_template);
            if (result is not SmsTemplateDto updated)
            {
                return;
            }

            updated.ApplyTo(entity);
            if (id == 0)
            {
                entity.SortOrder = nextSortOrder;
            }

            await db.InsertOrUpdateAsync(new[] { entity });
            await RefreshData();
        }

        private async Task OpenRuleDialogAsync(int id)
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
                rule = new RuleDto
                {
                    Description = "Description here",
                    Condition = RuleConditionType.DescriptionContains,
                    Created = DateTime.Now,
                    IsActive = true,
                };
            }

            var ruleVm = new RuleDialogVM(rule);
            var result = await dialogWrapper.ShowDialogAsync<RuleDialog>(ruleVm, 420, 440, LocalizationService.Instance.rule);
            if (result is not RuleDto updated)
            {
                return;
            }

            var updatedRule = new RuleModel
            {
                Id = id,
                Description = updated.Description,
                CategoryId = updated.CategoryId,
                Condition = updated.Condition,
                Created = updated.Created,
                IsActive = updated.IsActive,
                LocationId = updated.LocationId,
                PayeeId = updated.PayeeId,
                ProjectId = updated.ProjectId,
                MCCCategory = updated.MCCCategory,
            };

            int index = DbManual.Rules.FindIndex(r => r.Id == id);
            if (id != 0 && index >= 0)
            {
                DbManual.Rules[index] = updatedRule;
            }
            else
            {
                DbManual.Rules.Add(updatedRule);
            }

            await DbManual.SaveRulesAsync();
            await RefreshData();
        }
    }
}
