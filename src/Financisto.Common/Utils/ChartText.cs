using System.Text;
using System.Text.RegularExpressions;

namespace Financisto.Common.Utils
{
    /// <summary>Text for the charts.</summary>
    public static class ChartText
    {
        /// <summary>
        /// A user's text (a category or account name) made fit for a chart label, legend entry or tooltip, which
        /// LiveCharts draws itself. LiveCharts draws such a text with a single typeface and, when the text has a
        /// character the default typeface lacks, switches the whole text to a typeface that has it. A name like
        /// "Продукти🥗" thus gets the emoji font, which has no Cyrillic, and its letters turn into boxes. So emoji and
        /// other pictographs are dropped here; the grids and the pages' lists, which Avalonia draws, keep them.
        /// A text that is nothing but pictographs is returned as it is.
        /// </summary>
        public static string Label(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var label = new StringBuilder(text.Length);
            var dropped = false;
            foreach (var rune in text.EnumerateRunes())
            {
                if (IsPictograph(rune.Value))
                {
                    dropped = true;
                }
                else
                {
                    label.Append(rune.ToString());
                }
            }

            if (!dropped)
            {
                return text;
            }

            // "Кафе ☕ Бар" leaves two spaces behind
            var result = Regex.Replace(label.ToString(), " {2,}", " ").Trim();
            return result.Length == 0 ? text : result;
        }

        private static bool IsPictograph(int codePoint) =>
            codePoint >= 0x1F000                          // emoji, flags, and the rest of the astral pictographs
            || codePoint is >= 0x2300 and <= 0x23FF       // watch, hourglass, media controls...
            || codePoint is >= 0x2600 and <= 0x27BF       // weather, hearts, dingbats...
            || codePoint is >= 0x2B00 and <= 0x2BFF       // stars, large shapes...
            || codePoint is 0x200D or 0x20E3              // zero width joiner, keycap
            || codePoint is >= 0xFE00 and <= 0xFE0F;      // variation selectors
    }
}
