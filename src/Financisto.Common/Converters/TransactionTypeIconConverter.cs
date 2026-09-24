using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Financisto.Converters
{
    /// <summary>
    /// Replaces the WPF DataTemplate.Triggers (Type -> Image.Source) used by the type column;
    /// Avalonia data templates have no DataTemplate.Triggers, so this binds Source directly.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class TransactionTypeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = (value as string) switch
            {
                "Transfer" => "IconUpRightAndDownLeftFromCenter",
                "Income" => "IconSquareDownRight",
                "Share" => "IconShareNodesRed",
                _ => "IconSquareUpRight"
            };

            return Application.Current?.TryGetResource(key, null, out var resource) == true ? resource : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
