using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;

namespace Financisto.Common
{
    /// <summary>Reads and writes the import rules, which are the sms_template rows marked by <see cref="RuleSmsTemplateMapper.Marker"/>.</summary>
    [ExcludeFromCodeCoverage]
    public class RulesRepository
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly IFinancistoDatabase db;

        public RulesRepository(IFinancistoDatabase db)
        {
            this.db = db;
        }

        /// <summary>The stored rules in application order (oldest first); empty when they cannot be read.</summary>
        public async Task<List<RuleModel>> LoadAsync()
        {
            try
            {
                using var uow = db.CreateUnitOfWork();
                var templates = await uow.GetRepository<SmsTemplate>().GetAllAsync();
                return templates
                    .Where(RuleSmsTemplateMapper.IsRule)
                    .OrderBy(t => t.Id)
                    .Select(RuleSmsTemplateMapper.ToRule)
                    .ToList();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Error occurred while loading rules.");
                throw;
            }
        }

        /// <summary>
        /// Makes the stored rules equal <paramref name="rules"/>; real SMS templates are left untouched. A rule that is not stored
        /// yet gets its id from the database, so reload the rules afterwards.
        /// </summary>
        public async Task SaveAsync(IList<RuleModel> rules)
        {
            using var uow = db.CreateUnitOfWork();
            var repo = uow.GetRepository<SmsTemplate>();
            var storedRules = (await repo.GetAllAsync()).Where(RuleSmsTemplateMapper.IsRule).ToList();
            var storedIds = storedRules.Select(t => t.Id).ToHashSet();
            var keptIds = rules.Where(r => r.Id.HasValue).Select(r => r.Id!.Value).ToHashSet();

            foreach (var removed in storedRules.Where(t => !keptIds.Contains(t.Id)))
            {
                await repo.DeleteAsync(removed);
            }

            foreach (var rule in rules)
            {
                rule.UpdateTitles();
                var template = RuleSmsTemplateMapper.ToSmsTemplate(rule);
                if (storedIds.Contains(template.Id))
                {
                    await repo.UpdateAsync(template);
                }
                else
                {
                    template.Id = 0;
                    await repo.AddAsync(template);
                }
            }

            await uow.SaveChangesAsync();
        }
    }
}
