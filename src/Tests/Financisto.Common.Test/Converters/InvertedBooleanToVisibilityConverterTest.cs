namespace Financisto.Converters.Tests
{
    using System.Globalization;
    using AutoFixture.Xunit3;
    using Financisto.Converters;
    using Xunit;

    public class InvertedBooleanToVisibilityConverterTest
    {
        private readonly InvertedBooleanToVisibilityConverter converter = new InvertedBooleanToVisibilityConverter();

        [InlineAutoData(true, false)]
        [InlineAutoData(false, true)]
        [Theory]
        public void Convert_ValidParameters_OpositeValue(object value, bool expexted)
        {
            // Act
            var actual = converter.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

            Assert.Equal(expexted, actual);
        }

        [InlineAutoData(false, true)]
        [InlineAutoData(true, false)]
        [Theory]
        public void ConvertBack_ValidParameters_OpositeValue(object value, bool expexted)
        {
            // Act
            var actual = converter.ConvertBack(value, typeof(bool), null, CultureInfo.InvariantCulture);

            Assert.Equal(expexted, actual);
        }
    }
}
