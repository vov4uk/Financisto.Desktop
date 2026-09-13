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
            var title = values.Count > 0 && values[0] is string s ? s : string.Empty;
            var level = values.Count > 1 && values[1] is int i ? i : 0;
            return title.PadLeft(title.Length + level, '-');
        }
    }
}
