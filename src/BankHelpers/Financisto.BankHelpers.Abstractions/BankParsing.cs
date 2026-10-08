using System;
using System.Globalization;

namespace Financisto.BankHelpers
{
    /// <summary>Small parsing helpers shared by the statement readers.</summary>
    public static class BankParsing
    {
        private static readonly string[] DateTimeFormats = { "dd.MM.yyyy H:mm:ss", "dd.MM.yyyy H:mm" };

        /// <summary>Parses an amount with a decimal comma or point and optional thousands spaces; anything unparsable is 0.</summary>
        public static double GetDouble(string? text)
        {
            double.TryParse((text ?? string.Empty).Replace(',', '.').Replace(" ", string.Empty), NumberStyles.Any, NumberFormatInfo.InvariantInfo, out double value);
            return value;
        }

        /// <summary>Parses <c>dd.MM.yyyy H:mm[:ss]</c> (also with <c>H: mm</c>); an unparsable text is <see cref="DateTime.MinValue"/>.</summary>
        public static DateTime ParseDateTime(string? dateTime)
        {
            DateTime.TryParseExact((dateTime ?? string.Empty).Replace(": ", ":"), DateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value);
            return value;
        }
    }
}
