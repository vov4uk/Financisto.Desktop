using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Financisto.Common.Converters
{
    [ExcludeFromCodeCoverage]
    public class CategoryTitleConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            var title = (string)values[0];
            var level = (int)values[1];
            return (title ?? string.Empty).PadLeft((title ?? string.Empty).Length + level, '-');
        }
    }
}
