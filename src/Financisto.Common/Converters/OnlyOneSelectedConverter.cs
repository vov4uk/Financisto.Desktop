using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace Financisto.Converters
{
    public class OnlyOneSelectedConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            return values.Count(v => v is int intValue && intValue > 0) == 1;
        }
    }
}
