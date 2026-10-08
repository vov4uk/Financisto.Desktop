using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Financisto.Converters
{
    public class UnixTimeConverter : IValueConverter
    {
        public const string  FORMAT = "yyyy'-'MM'-'dd' 'HH':'mm':'ss";
        public const string  FORMAT_DAY = "yyyy'-'MM'-'dd";
        private static readonly DateTime StartDate = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        /// <summary>Format of a date with a time of day; a date at midnight is always shown as <see cref="FORMAT_DAY"/>.</summary>
        public string Format { get; set; } = FORMAT;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long timestamp = long.Parse(System.Convert.ToString(value) ?? string.Empty);
            var date = Convert(timestamp);
            if (date == date.Date)
            {
                return date.ToString(FORMAT_DAY);
            }
            return date.ToString(Format);
        }

        public static DateTime Convert(long timestamp)
        {
            return StartDate.AddMilliseconds(timestamp).ToLocalTime();
        }

        public static long ConvertBack(DateTime timestamp)
        {
            DateTimeOffset dto = new DateTimeOffset(timestamp);
            return dto.ToUnixTimeMilliseconds();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var dateStr = System.Convert.ToString(value);

            if (!DateTime.TryParseExact(dateStr, Format, null, DateTimeStyles.None, out var date))
            {
                if(!DateTime.TryParseExact(dateStr, FORMAT_DAY, null, DateTimeStyles.None, out date))
                {
                    date = (DateTime)value;
                }
            }

            return ConvertBack(date);
        }
    }
}
