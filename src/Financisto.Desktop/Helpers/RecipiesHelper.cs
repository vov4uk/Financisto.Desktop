using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Financisto.Desktop.Wizards.RecipesWizard.View;

namespace Financisto.Desktop.Helpers
{
    public static class RecipiesHelper
    {
        private const int maxLineLength = 150;
        private static readonly string[] LineSeparators = { "\r\n", "\n", "\r" };

        /// <summary>Splits on any line ending: pasted text isn't always in <see cref="Environment.NewLine"/> form.</summary>
        public static string[] SplitLines(string text, StringSplitOptions options = StringSplitOptions.None)
        {
            return text.Split(LineSeparators, options);
        }

        /// <summary>Puts every receipt amount at the end of its own line, joining the words in between.</summary>
        public static string FormatText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var array = SplitLines(text, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var singleLine = string.Join(' ', array);
            Regex numberRegex = new Regex(RecipesFormatter.Pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(1000));

            var matches = numberRegex.Matches(singleLine);

            int currentPosition = 0;

            StringBuilder sb = new StringBuilder();

            foreach (Match match in matches)
            {
                var line = singleLine.Substring(currentPosition, match.Index - currentPosition);
                currentPosition = match.Index + match.Length;

                string[] lines = line.Chunk(maxLineLength)
                    .Select(x => new string(x))
                    .ToArray();

                if (lines.Any())
                {
                    foreach (var item in lines.SkipLast(1))
                    {
                        sb.AppendLine(item.Trim());
                    }
                    sb.Append(lines[lines.Length - 1].TrimStart());
                }

                sb.AppendLine(match.Value.Replace(" ", "-"));
            }

            sb.AppendLine(singleLine.Substring(currentPosition).Trim());

            return sb.ToString();
        }
    }
}
