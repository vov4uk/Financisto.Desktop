namespace Financisto.Converters.Tests
{
    using System.Globalization;
    using AutoFixture.Xunit3;
    using Financisto.Converters;
    using Xunit;

    public class StringEmptyToVisibilityConverterTest
    {
        private readonly StringEmptyToVisibilityConverter converter = new StringEmptyToVisibilityConverter();

        [InlineAutoData("", true)]
        [InlineAutoData(null, true)]
        [InlineAutoData(123, true)]
        [InlineAutoData("xxx", false)]
        [Theory]
        public void Convert_GotParameters_ExpectedValues(object value, bool expexted)
        {
            // Act
            var actual = converter.Convert(value, typeof(bool), null, CultureInfo.InvariantCulture);

            Assert.Equal(expexted, actual);
        }

        [Fact]
        public void ConvertBack_NotSupported_ReturnNull()
        {
            // Act
            var actual = converter.ConvertBack(null, null, null, CultureInfo.InvariantCulture);

            Assert.Null(actual);
        }
    }
}
