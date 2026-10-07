using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Financisto.Desktop.Helpers;

namespace Financisto.Desktop.Wizards.RecipesWizard.View
{
    /// <summary>
    /// Receipt text → highlighted inlines for a TextBlock (Avalonia has no RichTextBox; this replaces the WPF
    /// RichTextBox formatter): words that are receipt amounts get yellow, other words with digits green.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class RecipesFormatter : IValueConverter
    {
        public const string Pattern = @"(((\+|\-)?)\d+(?:(\.|\,)?\d+))((\s+|-)(A|a|а|А|Б|б)|(ГБ))";
        private const string NumbersRegex = @"\d+";
        public const string Space = " ";

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return BuildInlines(value as string);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        public static InlineCollection BuildInlines(string? text)
        {
            var inlines = new InlineCollection();
            if (string.IsNullOrEmpty(text))
            {
                return inlines;
            }

            var lines = RecipiesHelper.SplitLines(text);
            for (int l = 0; l < lines.Length; l++)
            {
                var words = lines[l].Trim().Split(Space);
                for (int i = 0; i < words.Length; i++)
                {
                    string word = words[i];
                    if (Regex.IsMatch(word, Pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(1000)))
                    {
                        inlines.Add(GetRun(word, Brushes.DarkRed, Brushes.Yellow));
                        if (i != words.Length - 1) //not last word
                        {
                            inlines.Add(GetRun(Space, Brushes.DarkRed, Brushes.Yellow));
                        }
                    }
                    else
                    {
                        inlines.Add(GetRun(word, Brushes.DarkViolet, Brushes.LightGreen));
                        if (i != words.Length - 1) //not last word
                        {
                            inlines.Add(GetRun(Space, Brushes.DarkRed, Brushes.Yellow));
                        }
                    }
                }

                if (l != lines.Length - 1)
                {
                    inlines.Add(new LineBreak());
                }
            }

            return inlines;
        }

        private static Run GetRun(string word, IBrush foreground, IBrush background)
        {
            if (Regex.IsMatch(word, NumbersRegex, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(1000)))
            {
                return new Run(word) { Foreground = foreground, Background = background };
            }
            return new Run(word);
        }
    }
}
