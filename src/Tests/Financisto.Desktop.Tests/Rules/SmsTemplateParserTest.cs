namespace Financisto.Desktop.Tests.Rules
{
    using System;
    using Financisto.Common.Model;
    using Xunit;

    /// <summary>The template example check must extract the same values as the Android app's findTemplateMatches.</summary>
    public class SmsTemplateParserTest
    {
        [Fact]
        public void FindTemplateMatches_PlaceholdersInText_ExtractsValues()
        {
            var matches = SmsTemplateParser.FindTemplateMatches(
                "Card {{a}} purchase {{p}} {{f}} at {{e}}.",
                "Card 1234 purchase 12,50 USD at ALPHA SHOP. Thanks");

            Assert.NotNull(matches);
            Assert.Equal("1234", matches[(int)SmsPlaceholder.Account]);
            Assert.Equal("12,50", matches[(int)SmsPlaceholder.Price]);
            Assert.Equal("USD", matches[(int)SmsPlaceholder.Currency]);
            Assert.Equal("ALPHA SHOP", matches[(int)SmsPlaceholder.Payee]);
            Assert.Null(matches[(int)SmsPlaceholder.Project]);
        }

        [Fact]
        public void FindTemplateMatches_TemplateEndsWithPayee_CapturesToEndOfLine()
        {
            var matches = SmsTemplateParser.FindTemplateMatches("Paid {{p}} to {{e}}", "Paid 5.00 to NeoShop\nsecond line");

            Assert.NotNull(matches);
            Assert.Equal("NeoShop", matches[(int)SmsPlaceholder.Payee]);
        }

        [Fact]
        public void FindTemplateMatches_TextDoesNotMatch_ReturnsNull()
        {
            Assert.Null(SmsTemplateParser.FindTemplateMatches("Paid {{p}} to {{e}}.", "Something else"));
        }

        [Fact]
        public void FindTemplateMatches_NoPricePlaceholder_ReturnsNull()
        {
            Assert.Null(SmsTemplateParser.FindTemplateMatches("Paid to {{e}}.", "Paid to Shop."));
        }

        [Fact]
        public void FindTemplateMatches_SynonymsAreCaseInsensitive()
        {
            var matches = SmsTemplateParser.FindTemplateMatches("Paid {{P}} {{F}}.", "Paid 7 EUR.");

            Assert.NotNull(matches);
            Assert.Equal("EUR", matches[(int)SmsPlaceholder.Currency]);
        }

        [Theory]
        [InlineData("1423", "1423")]
        [InlineData("45.3", "45.3")]
        [InlineData("4.550.000", "4550000")]
        [InlineData("45,3", "45.3")]
        [InlineData("4,550,000", "4550000")]
        [InlineData("2.345,04", "2345.04")]
        [InlineData("2,345.04", "2345.04")]
        [InlineData("-12,50", "-12.50")]
        [InlineData("(12.50)", "-12.50")]
        [InlineData("", "0")]
        public void ToDecimal_Formats_ParsedLikeAndroid(string value, string expected)
        {
            Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), SmsTemplateParser.ToDecimal(value));
        }

        [Fact]
        public void ToDecimal_NotANumber_Throws()
        {
            Assert.Throws<FormatException>(() => SmsTemplateParser.ToDecimal("..."));
        }
    }
}
