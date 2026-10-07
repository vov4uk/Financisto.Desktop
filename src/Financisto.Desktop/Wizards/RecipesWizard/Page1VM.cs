using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Financisto.Common.Entities;
using Financisto.Common.Utils;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Wizards.RecipesWizard.View;
using Prism.Commands;

namespace Financisto.Desktop.Wizards.RecipesWizard.ViewModel
{
    public class Page1VM : RecipesWizardPageVMBase
    {
        //TODO fix '100600 Балтика' case then it calculates (100600 Б), no more symbols after a|b
        private readonly string pattern = RecipesFormatter.Pattern + @"(\t|\n|\r|$)";
        private readonly char[] charactersToRemove = { ':', '/', '?', '#', '[', ']', '@', '*', '.', ',', '\"', '&', '\'' };
        private DelegateCommand _highlightCommand;
        private string text;
        public Page1VM(double totalAmount)
        {
            TotalAmount = totalAmount;
        }

        public List<FinancistoTransactionDto> Amounts { get; } = new List<FinancistoTransactionDto>();

        public DelegateCommand HighlightCommand => _highlightCommand ??= new DelegateCommand(HighLight);

        public string Text
        {
            get => this.text;
            set
            {
                this.text = value;
                this.RaisePropertyChanged(nameof(this.Text));
            }
        }

        public override string Title => "Paste text";

        public void CalculateCurrentAmount()
        {
            Amounts.Clear();

            if (!string.IsNullOrEmpty(text))
            {
                double tmp = 0.0;
                int order = 1;
                var lines = RecipiesHelper.SplitLines(text).Where(line => !string.IsNullOrWhiteSpace(line));
                foreach (var line in lines)
                {
                    var findNumber = Regex.Match(line, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(1000));
                    if (findNumber.Success)
                    {
                        var amount = GetDouble(findNumber.Value.Substring(0, findNumber.Value.Length - 2).Replace(",", ".").Trim());
                        tmp += amount;

                        if (DoubleUtils.DoubleNotEqual(amount, 0.0))
                        {
                            var note = line.Replace(findNumber.Value, string.Empty);
                            var lineResult = ParseLine(note);

                            Amounts.Add(new FinancistoTransactionDto
                            {
                                FromAmount = Convert.ToInt64(amount * -100.0),
                                Note = lineResult.note,
                                CategoryId = lineResult.categoryId,
                                Order = order++
                            });
                        }
                    }
                }

                CalculatedAmount = Math.Abs(tmp) * -1.0;
            }
        }

        public override bool IsValid() => !string.IsNullOrWhiteSpace(Text);

        private static double GetDouble(string value, double defaultValue = 0.0)
        {
            double result;
            if (!double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result) &&
                !double.TryParse(value, NumberStyles.Any, CultureInfo.GetCultureInfo("en-US"), out result) &&
                !double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            {
                result = defaultValue;
            }
            return result;
        }

        private void HighLight()
        {
            Text = RecipiesHelper.FormatText(Text);
            CalculateCurrentAmount();
        }

        private static bool ContainsString(string title, string[] description)
        {
            if (!string.IsNullOrEmpty(title) && description != null)
            {
                return description.Any(x => title.Contains(x, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        private static void TryParseCategory(string[] desc, out int categoryId)
        {
            var category = DbManual.Category
                    .Where(x => x.Id > 0)
                    .FirstOrDefault(l => ContainsString(l.Title, desc));
            categoryId = category?.Id ?? 0;
        }

        private (string note, int categoryId) ParseLine(string note)
        {
            int categoryId = 0;
            if (!string.IsNullOrWhiteSpace(note))
            {
                foreach (char c in charactersToRemove)
                {
                    note = note.Replace(c.ToString(), string.Empty);
                }

                var arr = note.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                              .Where(x => x.Length > 2)
                              .Select(x => x.Trim('-').Trim().ToLowerInvariant())
                              .ToArray();

                TryParseCategory(arr, out categoryId);

                note = string.Join(" ", arr);
            }
            note = string.IsNullOrWhiteSpace(note) ? string.Empty : note.TrimEnd();
            return (note, categoryId);
        }
    }
}
