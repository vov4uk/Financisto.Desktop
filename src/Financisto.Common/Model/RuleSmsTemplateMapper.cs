using System;
using System.Diagnostics.CodeAnalysis;
using Financisto.Common.Entities;
using Financisto.DataAccess.Data;

namespace Financisto.Common.Model
{
    /// <summary>
    /// Stores an import rule in the sms_template table so it travels with the backup.
    /// The condition is encoded into description as "contains:lidl", "matches:lidl" or "mcc:&lt;Mcc name&gt;".
    /// The template column holds <see cref="Marker"/>, which never occurs in an SMS, so the Android app never matches it
    /// while the desktop app tells its own rows from real SMS templates.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public static class RuleSmsTemplateMapper
    {
        public const string Marker = "financisto.desktop.rule";

        private const string ContainsPrefix = "contains";
        private const string MatchesPrefix = "matches";
        private const string MccPrefix = "mcc";

        public static bool IsRule(SmsTemplate template) => template?.Template == Marker;

        public static SmsTemplate ToSmsTemplate(RuleModel rule)
        {
            return new SmsTemplate
            {
                Id = rule.Id ?? 0,
                IsActive = rule.IsActive,
                Title = rule.Title ?? string.Empty,
                Description = EncodeCondition(rule),
                Template = Marker,
                AccountId = -1,
                CategoryId = rule.CategoryId ?? 0,
                PayeeId = rule.PayeeId ?? 0,
                ProjectId = rule.ProjectId ?? 0,
                LocationId = rule.LocationId ?? 0,
                UpdatedOn = new DateTimeOffset(rule.Created == default ? DateTime.Now : rule.Created).ToUnixTimeMilliseconds(),
            };
        }

        public static RuleModel ToRule(SmsTemplate template)
        {
            var rule = new RuleModel
            {
                Id = template.Id,
                IsActive = template.IsActive,
                CategoryId = NullIfZero(template.CategoryId),
                PayeeId = NullIfZero(template.PayeeId),
                ProjectId = NullIfZero(template.ProjectId),
                LocationId = NullIfZero(template.LocationId),
                Created = template.UpdatedOn > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(template.UpdatedOn).LocalDateTime
                    : DateTime.Now,
            };

            DecodeCondition(template.Description, rule);
            return rule;
        }

        internal static string EncodeCondition(RuleModel rule)
        {
            return rule.Condition switch
            {
                RuleConditionType.MCC => $"{MccPrefix}:{rule.MCCCategory}",
                RuleConditionType.DescriptionMatches => $"{MatchesPrefix}:{rule.Description}",
                _ => $"{ContainsPrefix}:{rule.Description}",
            };
        }

        internal static void DecodeCondition(string description, RuleModel rule)
        {
            description ??= string.Empty;
            int separator = description.IndexOf(':');
            string prefix = separator > 0 ? description[..separator].Trim() : string.Empty;
            string value = separator > 0 ? description[(separator + 1)..] : description;

            if (prefix.Equals(MccPrefix, StringComparison.OrdinalIgnoreCase))
            {
                rule.Condition = RuleConditionType.MCC;
                rule.MCCCategory = Enum.TryParse<Mcc>(value.Trim(), true, out var mcc) ? mcc : Mcc.none;
                rule.Description = string.Empty;
            }
            else if (prefix.Equals(MatchesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                rule.Condition = RuleConditionType.DescriptionMatches;
                rule.Description = value;
            }
            else if (prefix.Equals(ContainsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                rule.Condition = RuleConditionType.DescriptionContains;
                rule.Description = value;
            }
            else
            {
                rule.Condition = RuleConditionType.DescriptionContains;
                rule.Description = description;
            }
        }

        private static int? NullIfZero(int value) => value > 0 ? value : null;
    }
}
