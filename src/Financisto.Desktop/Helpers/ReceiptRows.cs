using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Financisto.Desktop.Helpers;

/// <summary>One purchased item of a receipt table: what it was and what it cost after the discount (positive = spent).</summary>
public readonly record struct ReceiptItem(string Name, double Amount);

/// <summary>
/// A receipt printed as a table (Polish "paragon", e.g. Biedronka): one row per item, <c>Name PTU quantity x price value</c>
/// (<c>PTU</c> is the tax letter or "brak"), then an optional <c>Rabat -discount</c> row and the net amount of the item alone on a line:
/// <code>
/// SokJabtkRivPet1l  C  6.000 x 2,99  17,94
/// Rabat -3,60
/// 14,34
/// </code>
/// The item costs value + discount; the line with the net amount only repeats it. The other lines (header, tax summary, payment) are not items.
/// A second layout has the tax letter last and a <c>*</c> for the times, usually with the name alone on the line above:
/// <code>
/// Krasnystaw Kefir 2%
///                        2 * 2.89 5.78 C
/// </code>
/// Lidl prints that layout, and its discounts are lines of free text with a negative amount under the item
/// (<c>Taniej za 2   -20,00</c>, <c>Lidl Plus voucher   -1,76</c>), not a <c>Rabat</c> row; a returnable deposit is a row with the name and no tax letter
/// (<c>Kaucja PET   1 * 0.5 0.5</c>).
/// The marked-amount format ("12,50 A") is the other kind of receipt, see <see cref="Wizards.RecipesWizard.View.RecipesFormatter.Pattern"/>.
/// </summary>
public static class ReceiptRows
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(1000);
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // The name may be missing (it was printed on the line above); OCR can read the "x" as × or X; the quantity has up to three decimals (a weight)
    // Name, tax letter (or "brak"), quantity x price, value
    private static readonly Regex ItemRow = new(
        @"^\s*(?:(?<name>.*?)\s+)?(?<ptu>[A-Za-z]|brak)\s+\d+(?:[.,]\d{1,3})?\s*[x×*]\s*\d+[.,]\d{2}\s+(?<value>-?\d+[.,]\d{2})\s*$", Options, Timeout);

    // The other layout: name, quantity * price, value, tax letter
    private static readonly Regex ItemRowTaxLast = new(
        @"^\s*(?:(?<name>.*?)\s+)?\d+(?:[.,]\d{1,3})?\s*[x×*]\s*\d+[.,]\d{2}\s+(?<value>-?\d+[.,]\d{2})\s*(?<ptu>[A-Za-z])\s*$", Options, Timeout);

    // A row without a tax letter, with its name on the same line
    private static readonly Regex ItemRowNoTax = new(
        @"^\s*(?<name>\S.*?)\s+\d+(?:[.,]\d{1,3})?\s*[x×*]\s*\d+[.,]\d{1,2}\s+(?<value>-?\d+[.,]\d{1,2})\s*$", Options, Timeout);

    // "Rabat" with an amount (any sign), or a line of any text ending with a negative amount ("Taniej za 2   -20,00"); it counts under an item
    private static readonly Regex DiscountRow = new(
        @"^\s*(?:Rabat\w*\s+(?<discount>-?\d+[.,]\d{2})|(?:.*\S\s+)?(?<discount>-\d+[.,]\d{2}))\s*$", Options, Timeout);

    private static readonly Regex NetAmountRow = new(@"^\s*\d+[.,]\d{2}\s*$", Options, Timeout);

    public static bool IsItemRow(string line) => MatchItem(line).Success;

    /// <summary>An item row that has no name of its own: the name is the text line above it.</summary>
    public static bool IsNamelessItemRow(string line)
    {
        var item = MatchItem(line);
        return item.Success && item.Groups["name"].Value.Trim().Length == 0;
    }

    /// <summary>
    /// The line at <paramref name="index"/> is the name of the item row that follows it (the next line with text is a row without a name):
    /// the preview shows it as part of the row.
    /// </summary>
    public static bool IsNameOfNextItemRow(IReadOnlyList<string> lines, int index)
    {
        var line = lines[index].Trim();
        if (line.Length == 0 || IsItemRow(line) || IsDiscountRow(line) || NetAmountRow.IsMatch(line))
        {
            return false;
        }

        var next = lines.Skip(index + 1).FirstOrDefault(other => other.Trim().Length > 0);
        return next is not null && IsNamelessItemRow(next);
    }

    public static bool IsDiscountRow(string line) => DiscountRow.IsMatch(line);

    /// <summary>The text is a receipt table: at least one of its lines is an item row.</summary>
    public static bool IsReceiptTable(IEnumerable<string> lines) => lines.Any(IsItemRow);

    /// <summary>The items of the receipt in print order, each with its discount taken off.</summary>
    public static List<ReceiptItem> Parse(IEnumerable<string> lines)
    {
        var items = new List<ReceiptItem>();
        string? previousText = null; // a name printed on its own line, before the row that has the numbers
        var afterItem = false;       // a discount belongs to the item right above it

        foreach (var line in lines.Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            var item = MatchItem(line);
            if (item.Success)
            {
                var name = item.Groups["name"].Value.Trim();
                items.Add(new ReceiptItem(name.Length > 0 ? name : previousText ?? string.Empty, ToDouble(item.Groups["value"].Value)));
                previousText = null;
                afterItem = true;
                continue;
            }

            var discount = DiscountRow.Match(line);
            if (discount.Success && afterItem)
            {
                // printed with a minus; after "Rabat" a plain number is a discount too
                var last = items[^1];
                items[^1] = last with { Amount = Math.Round(last.Amount - Math.Abs(ToDouble(discount.Groups["discount"].Value)), 2) };
                continue;
            }

            if (NetAmountRow.IsMatch(line))
            {
                continue; // the net amount of the item above: already counted
            }

            previousText = line;
            afterItem = false;
        }

        return items;
    }

    private static Match MatchItem(string line)
    {
        var item = ItemRow.Match(line);
        if (!item.Success)
        {
            item = ItemRowTaxLast.Match(line);
        }

        return item.Success ? item : ItemRowNoTax.Match(line);
    }

    private static double ToDouble(string number) => double.Parse(number.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);
}
