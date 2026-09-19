using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Financisto.Converters
{
    /// <summary>
    /// Replaces the WPF DataTemplate.Triggers (Type -> Foreground) used by the amount column;
    /// Avalonia data templates have no DataTemplate.Triggers, so this binds Foreground directly.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class TransactionTypeBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as string) switch
            {
                "Transfer" => Brushes.DarkOrange,
                "Income" => Brushes.DarkGreen,
                "Share" => Brushes.DarkRed,
                "Expense" => Brushes.DarkRed,
                _ => Brushes.Black
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
