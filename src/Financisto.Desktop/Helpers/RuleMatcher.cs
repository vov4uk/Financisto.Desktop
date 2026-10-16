using System;
using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.Desktop.Wizards;

namespace Financisto.Desktop.Helpers;

/// <summary>
/// Applies the import rules (<see cref="DbManual.Rules"/>) to a row of the import wizard: a rule whose condition the row meets
/// fills its category, location, payee and project. The rules go in order, so a later rule wins over an earlier one.
/// </summary>
public static class RuleMatcher
{
    /// <summary>Applies every active rule.</summary>
    public static void Apply(FinancistoTransactionDto transaction) => Apply(transaction, DbManual.Rules);

    public static void Apply(FinancistoTransactionDto transaction, IEnumerable<RuleModel> rules)
    {
        foreach (var rule in rules.Where(r => r.IsActive && Matches(r, transaction)))
        {
            if (rule.CategoryId.HasValue)
            {
                transaction.CategoryId = rule.CategoryId.Value;
            }

            if (rule.LocationId.HasValue)
            {
                transaction.LocationId = rule.LocationId.Value;
            }

            if (rule.PayeeId.HasValue)
            {
                transaction.PayeeId = rule.PayeeId.Value;
            }

            if (rule.ProjectId.HasValue)
            {
                transaction.ProjectId = rule.ProjectId.Value;
            }
        }
    }

    private static bool Matches(RuleModel rule, FinancistoTransactionDto transaction)
    {
        switch (rule.Condition)
        {
            case RuleConditionType.DescriptionContains:
                return !string.IsNullOrEmpty(rule.Description) && transaction.Note?.Contains(rule.Description, StringComparison.OrdinalIgnoreCase) == true;
            case RuleConditionType.DescriptionMatches:
                return !string.IsNullOrEmpty(rule.Description) && transaction.Note?.Equals(rule.Description, StringComparison.OrdinalIgnoreCase) == true;
            case RuleConditionType.MCC:
                return transaction.MCC > 0 && DbManual.MCCEnums.TryGetValue(rule.MCCCategory, out var codes) && codes.Contains(transaction.MCC);
            default:
                return false;
        }
    }
}
