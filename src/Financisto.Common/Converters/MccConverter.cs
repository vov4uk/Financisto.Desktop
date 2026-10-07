using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Financisto.Common.Entities;

namespace Financisto.Converters
{
    public class MccConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int code = (int)value;
            if (DbManual.MCCCodes.TryGetValue(code, out var mccValue))
            {
                // WPF showed the enum through its TypeConverter (the localized title); Avalonia uses ToString(), so return the title for text targets.
                return targetType == typeof(string) ? mccValue.GetEnumLocalizedMccDescription() : mccValue;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
