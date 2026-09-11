using System;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;

namespace Financisto.Converters
{
    /// <summary>
    /// Binds to a control's IsVisible property (Avalonia has no Visibility enum).
    /// </summary>
    public class StringEmptyToVisibilityConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string);
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
