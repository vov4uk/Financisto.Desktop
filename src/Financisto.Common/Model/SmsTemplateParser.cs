using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Financisto.Common.Model
{
    /// <summary>Placeholders of an SMS template. The declaration order matters: it is the index into the match array.</summary>
    public enum SmsPlaceholder
    {
        Any,
        Account,
        Balance,
        AccountName,
        Date,
        Payee,
        Currency,
        TimestampMillis,
        Price,
        Project,
        Text,
        GreedyText,
        TransferToAccountName,
    }

    /// <summary>
    /// Port of the Android app's SmsTransactionProcessor.findTemplateMatches, so the example field of the template dialog
    /// shows what the phone will extract. Keep it in sync with the Android version.
    /// </summary>
    public static class SmsTemplateParser
    {
        private const string EndOfLine = "(?=[\\r\\n]|$)";
        private const string BalanceRegex = "\\s{0,3}([\\d\\.,\\-\\+\\']+(?:[\\d \\xA0\\.,]+?)*)\\s{0,3}";

        private static readonly (string Code, string Regex, string Synonym)[] Placeholders =
        [
            ("<::>", ".*?", "{{*}}"),
            ("<:A:>", "\\s{0,3}(\\d{4})\\s{0,3}", "{{a}}"),
            ("<:B:>", BalanceRegex, "{{b}}"),
            ("<:C:>", "(\\S+?)", "{{c}}"),
            ("<:D:>", "\\s{0,3}(\\d[\\d\\. /:-]{12,14}\\d)\\s*?", "{{d}}"),
            ("<:E:>", "([^\\r\\n]+?)", "{{e}}"),
            ("<:F:>", "([A-Z]{3})", "{{f}}"),
            ("<:G:>", "(\\d{1,13})", "{{g}}"),
            ("<:P:>", BalanceRegex, "{{p}}"),
            ("<:R:>", "(\\S+?)", "{{r}}"),
            ("<:T:>", "(.*?)", "{{t}}"),
            ("<:U:>", "(.*)", "{{u}}"),
            ("<:X:>", "(\\S+?)", "{{x}}"),
        ];

        public static int PlaceholderCount => Placeholders.Length;

        /// <summary>The values extracted per <see cref="SmsPlaceholder"/>, or null when the template does not match the text.</summary>
        public static string[] FindTemplateMatches(string template, string sms)
        {
            if (string.IsNullOrEmpty(template))
            {
                return null;
            }

            template = PreprocessPatterns(template);
            int[] indexes = FindPlaceholderIndexes(template);
            if (indexes == null)
            {
                return null;
            }

            // A lazy capture with nothing after it has no closing anchor and would settle for one character,
            // so a template ending with the placeholder (optionally followed by {{*}}) extends it to the end of the line.
            string any = Placeholders[(int)SmsPlaceholder.Any].Code;
            bool endsWithLazy = EndsWithLazyCapture(template);
            bool endsWithLazyThenAny = !endsWithLazy
                && template.EndsWith(any, StringComparison.Ordinal)
                && EndsWithLazyCapture(template[..^any.Length]);

            template = Regex.Replace(template, @"([.\[\]{}()*+\-?^$|\\])", @"\$1");
            for (int i = 0; i < indexes.Length; i++)
            {
                if (indexes[i] != -1)
                {
                    template = template.Replace(Placeholders[i].Code, Placeholders[i].Regex, StringComparison.Ordinal);
                }
            }

            string anyRegex = Placeholders[(int)SmsPlaceholder.Any].Regex;
            template = template.Replace(any, anyRegex, StringComparison.Ordinal);
            if (endsWithLazy)
            {
                template += EndOfLine;
            }
            else if (endsWithLazyThenAny)
            {
                template = template[..^anyRegex.Length] + EndOfLine + anyRegex;
            }

            try
            {
                Match match = Regex.Match(sms ?? string.Empty, template, RegexOptions.Singleline);
                if (!match.Success)
                {
                    return null;
                }

                var results = new string[Placeholders.Length];
                for (int i = 0; i < indexes.Length; i++)
                {
                    int group = indexes[i] + 1;
                    if (group > 0)
                    {
                        results[i] = match.Groups[group].Value;
                    }
                }

                return results;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>Port of SmsTransactionProcessor.toBigDecimal; throws FormatException for an unrecognised number.</summary>
        [ExcludeFromCodeCoverage]
        public static decimal ToDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0m;
            }

            string trimmed = value.Trim();
            bool negative = (trimmed.Contains('(') && trimmed.Contains(')')) || trimmed.EndsWith('-') || trimmed.StartsWith('-');
            string parsed = Regex.Replace(value, "[^0-9,.]", string.Empty);
            if (negative)
            {
                parsed = "-" + parsed;
            }

            int lastPoint = parsed.LastIndexOf('.');
            int lastComma = parsed.LastIndexOf(',');

            if (lastPoint == -1 && lastComma == -1)
            {
                return Parse(parsed);
            }

            if (lastPoint > -1 && lastComma == -1)
            {
                return parsed.IndexOf('.') != lastPoint ? Parse(parsed.Replace(".", string.Empty)) : Parse(parsed);
            }

            if (lastPoint == -1)
            {
                // Only commas: assume the decimal part has at most 2 digits.
                return parsed.IndexOf(',') != lastComma || lastComma < parsed.Length - 3
                    ? Parse(parsed.Replace(",", string.Empty))
                    : Parse(parsed.Replace(',', '.'));
            }

            return lastPoint < lastComma
                ? Parse(parsed.Replace(".", string.Empty).Replace(',', '.'))
                : Parse(parsed.Replace(",", string.Empty));
        }

        private static decimal Parse(string value)
        {
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result))
            {
                throw new FormatException($"Unexpected number format. Cannot convert '{value}' to decimal.");
            }

            return result;
        }

        private static string PreprocessPatterns(string template)
        {
            foreach (var placeholder in Placeholders)
            {
                template = template.Replace(placeholder.Synonym, placeholder.Code, StringComparison.OrdinalIgnoreCase);
            }

            return template;
        }

        /// <summary>The group index of each placeholder in first-occurrence order, or null when the template has no price.</summary>
        private static int[] FindPlaceholderIndexes(string template)
        {
            var sorted = new SortedDictionary<int, int>();
            bool foundPrice = false;
            for (int p = 0; p < Placeholders.Length; p++)
            {
                int index = template.IndexOf(Placeholders[p].Code, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                if (p == (int)SmsPlaceholder.Price)
                {
                    foundPrice = true;
                }

                if (p != (int)SmsPlaceholder.Any)
                {
                    sorted[index] = p;
                }
            }

            if (!foundPrice)
            {
                return null;
            }

            int[] result = Enumerable.Repeat(-1, Placeholders.Length).ToArray();
            int order = 0;
            foreach (int p in sorted.Values)
            {
                result[p] = order++;
            }

            return result;
        }

        private static bool EndsWithLazyCapture(string template)
        {
            foreach (var placeholder in Placeholders)
            {
                if (template.EndsWith(placeholder.Code, StringComparison.Ordinal))
                {
                    return placeholder.Regex.EndsWith("?)", StringComparison.Ordinal);
                }
            }

            return false;
        }
    }
}
