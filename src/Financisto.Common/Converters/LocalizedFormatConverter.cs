using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Financisto.Common.Converters
{
    /// <summary>
    /// Combines a localized label (values[0]) with an optional dynamic value (values[1]).
    /// Returns "Label (value)" when value is non-empty, or just "Label" when value is null/empty.
    /// Bind values[0] to a LocalizationService indexer so the text reacts to culture changes.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class LocalizedFormatConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Count == 0) return string.Empty;
            var first = values[0] as string ?? string.Empty;
            if (values.Count == 1) return first;

            // 2 values: "Label (value)" pattern — returns just label when value is null/empty
            if (values.Count == 2)
            {
                var val = values[1] as string;
                return string.IsNullOrEmpty(val) ? first : $"{first} ({val})";
            }

            // 3+ values: first is a format template, rest are positional args
            var args = new object[values.Count - 1];
            for (var i = 0; i < args.Length; i++)
            {
                args[i] = values[i + 1];
            }
            return string.Format(first, args);
        }
    }
}
