using System;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.Desktop.Data;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.Views.Dialogs;

namespace Financisto.Desktop.Helpers;

/// <summary>Adding an import rule from a wizard: the rule dialog, and storing what the wizard added once it is closed.</summary>
internal static class RuleCreator
{
    /// <summary>Shows the rule dialog for a new rule that starts with <paramref name="description"/> ("contains" condition) and adds the rule to
    /// <see cref="DbManual.Rules"/>; null when the dialog is cancelled. The rule has no id until <see cref="SaveNewAsync"/>.</summary>
    /// <param name="mccCode">The merchant code of the row (import), preselected for an MCC condition; 0 = none.</param>
    public static async Task<RuleModel?> AddRuleAsync(IDialogWrapper dialogWrapper, string? description, int mccCode = 0)
    {
        var rule = new RuleDto
        {
            Description = description!,
            Condition = RuleConditionType.DescriptionContains,
            Created = DateTime.Now,
            IsActive = true,
            MCCCategory = DbManual.MCCCodes.TryGetValue(mccCode, out var mcc) ? mcc : Mcc.none,
        };

        var result = await dialogWrapper.ShowDialogAsync<RuleDialog>(new RuleDialogVM(rule), 420, 440, LocalizationService.Instance.rule);
        if (result is not RuleDto updated)
        {
            return null;
        }

        var model = updated.ToModel();
        DbManual.Rules.Add(model);
        return model;
    }

    /// <summary>
    /// The wizards only add new rules to <see cref="DbManual.Rules"/> (they have no database): store them and reload, to get their ids.
    /// Does nothing when there is no new rule.
    /// </summary>
    public static async Task SaveNewAsync(IFinancistoDatabase db)
    {
        if (DbManual.Rules.Any(r => !(r.Id > 0)))
        {
            await new RulesRepository(db).SaveAsync(DbManual.Rules);
            DbManual.ResetManuals(nameof(DbManual.Rules));
            await DbManual.SetupAsync(db);
        }
    }
}
