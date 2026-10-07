using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Financisto.Converters
{
    /// <summary>
    /// Import wizard row highlight, replacing the WPF DataTriggers on the date cell:
    /// orange for a transfer to/from an account in another currency (<see cref="DifferentCurrencyConverter"/>),
    /// pink unless exactly one of From account / To account / Category is chosen (<see cref="OnlyOneSelectedConverter"/>).
    /// Values: FromAccountId, ToAccountId, CategoryId, the imported account.
    /// </summary>
    public class ImportRowBackgroundConverter : IMultiValueConverter
    {
        private readonly DifferentCurrencyConverter differentCurrency = new();
        private readonly OnlyOneSelectedConverter onlyOneSelected = new();

        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (differentCurrency.Convert(values, targetType, parameter, culture) is true)
            {
                return Brushes.Orange;
            }

            if (onlyOneSelected.Convert(values.Take(3).ToList(), targetType, parameter, culture) is false)
            {
                return Brushes.Pink;
            }

            return Brushes.Transparent;
        }
    }
}
