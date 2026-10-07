namespace Financisto.Converters.Tests
{
    using System;
    using System.Globalization;
    using AutoFixture.Xunit3;
    using Financisto.Converters;
    using Xunit;

    // Cant use constants as expexted value because Azure Pipeline run test in different time zone
    public class UnixTimeConverterTest
    {
        private static readonly DateTime StartDate = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        private readonly UnixTimeConverter converter = new UnixTimeConverter();

        [InlineAutoData(1642152563000)]
        [InlineAutoData(1642111200000)]
        [Theory]
        public void Convert_GotParameters_ExpectedValues(long value)
        {
            string exp;
            DateTime date = StartDate.AddMilliseconds(value).ToLocalTime();

            exp = date == date.Date ? date.ToString(UnixTimeConverter.FORMAT_DAY) : date.ToString(UnixTimeConverter.FORMAT);

            // Act
            var actual = converter.Convert(value, null, null, CultureInfo.InvariantCulture);

            Assert.Equal(exp, actual);
        }

        [Fact]
        public void Convert_CustomFormat_UsedForTimeOfDay()
        {
            var custom = new UnixTimeConverter { Format = "yyyy'-'MM'-'dd' 'HH':'mm" };
            var local = new DateTime(2022, 1, 14, 11, 29, 23, DateTimeKind.Local);
            long value = new DateTimeOffset(local).ToUnixTimeMilliseconds();

            var actual = custom.Convert(value, null, null, CultureInfo.InvariantCulture);

            Assert.Equal("2022-01-14 11:29", actual);
        }

        [Fact]
        public void Convert_CustomFormat_MidnightStillShownAsDay()
        {
            var custom = new UnixTimeConverter { Format = "yyyy'-'MM'-'dd' 'HH':'mm" };
            var local = new DateTime(2022, 1, 14, 0, 0, 0, DateTimeKind.Local);
            long value = new DateTimeOffset(local).ToUnixTimeMilliseconds();

            var actual = custom.Convert(value, null, null, CultureInfo.InvariantCulture);

            Assert.Equal("2022-01-14", actual);
        }

        [InlineAutoData("2022-01-14 11:29:23")]
        [InlineAutoData("2022-01-14")]
        [Theory]
        public void ConvertBack_GotParameters_ExpectedValues(string value)
        {
            DateTime dateTime = DateTime.Parse(value).ToUniversalTime();
            DateTimeOffset dto = new DateTimeOffset(dateTime);
            long expexted = dto.ToUnixTimeMilliseconds();

            // Act
            var actual = converter.ConvertBack(dateTime, null, null, CultureInfo.InvariantCulture);

            Assert.Equal(expexted, actual);
        }

        [InlineAutoData("xxx")]
        [InlineAutoData(null)]
        [Theory]
        public void Convert_InvalidParameters_ThrowsException(object value)
        {
            // Act
            Assert.Throws<FormatException>(() => converter.Convert(value, null, null, CultureInfo.InvariantCulture));
        }
    }
}
