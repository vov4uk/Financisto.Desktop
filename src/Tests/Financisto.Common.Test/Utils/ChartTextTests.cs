namespace Financisto.Converters.Tests
{
    using Financisto.Common.Utils;
    using Xunit;

    public class ChartTextTests
    {
        [Theory]
        [InlineData("Продукти🥗", "Продукти")]
        [InlineData("Food🥗🍌", "Food")]
        [InlineData("Car 🚗", "Car")]
        [InlineData("Громадський транспорт🚋", "Громадський транспорт")]
        [InlineData("Żywność🥖", "Żywność")]
        [InlineData("日本語🍣", "日本語")]
        [InlineData("Здоров'я ❤️", "Здоров'я")]
        [InlineData("👨‍👩‍👧 Сім'я", "Сім'я")]
        [InlineData("🇺🇦 Україна", "Україна")]
        [InlineData("Кафе ☕ Бар", "Кафе Бар")]
        [InlineData("🥗 Салат", "Салат")]
        public void Label_TextWithEmoji_DropsTheEmoji(string text, string expected)
        {
            Assert.Equal(expected, ChartText.Label(text));
        }

        [Theory]
        [InlineData("Продукти")]
        [InlineData("Food")]
        [InlineData("Public transport")]
        [InlineData("Tax & fees (€, $, %)")]
        [InlineData("A → B")]
        [InlineData("Copyright ©")]
        [InlineData("<NO_CATEGORY>")]
        [InlineData("  padded  ")]
        [InlineData("")]
        public void Label_TextWithoutEmoji_IsReturnedUnchanged(string text)
        {
            Assert.Equal(text, ChartText.Label(text));
        }

        [Theory]
        [InlineData("🥗")]
        [InlineData("  🥗🍌  ")]
        public void Label_OnlyEmoji_IsReturnedUnchanged(string text)
        {
            Assert.Equal(text, ChartText.Label(text));
        }

        [Fact]
        public void Label_Null_ReturnsNull()
        {
            Assert.Null(ChartText.Label(null));
        }
    }
}
