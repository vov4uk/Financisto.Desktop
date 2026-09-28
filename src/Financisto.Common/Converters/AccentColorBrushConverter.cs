using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Financisto.Common.Utils;

namespace Financisto.Converters
{
    /// <summary>
    /// Account accent color code -> the highlight behind the account icon, like Android's AccountRecyclerAdapter:
    /// a left-to-right gradient from the color to transparent. Null (no highlight) when the code is empty or invalid.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class AccentColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!AndroidColor.TryParse(value as string, out var color))
                return null;

            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(color, 0),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
                },
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
