namespace Financisto.Desktop.Tests.Pages.Dialog
{
    using Financisto.DataAccess.Data;
    using Financisto.Desktop.Data;
    using Financisto.Desktop.ViewModels.Dialogs;
    using Xunit;

    /// <summary>The database keeps the separators as quoted characters; the dialog must not leak the enum member names into it.</summary>
    public class CurrencyDialogVMTest
    {
        [Theory]
        [InlineData(DecimalSeparator.PERIOD, GroupSeparator.COMMA, "'.'", "','")]
        [InlineData(DecimalSeparator.COMMA, GroupSeparator.SPACE, "','", "' '")]
        [InlineData(DecimalSeparator.SPACE, GroupSeparator.PERIOD, "' '", "'.'")]
        [InlineData(DecimalSeparator.COMMA, GroupSeparator.NONE, "','", "''")]
        public void OnRequestSave_WritesQuotedSeparators(DecimalSeparator decimalSeparator, GroupSeparator groupSeparator, string expectedDecimal, string expectedGroup)
        {
            var vm = new CurrencyDialogVM(new CurrencyDto(new Currency { Decimals = 2, DecimalSeparator = "'.'", GroupSeparator = "','" }))
            {
                SelectedDecimalSeparator = decimalSeparator,
                SelectedGroupSeparator = groupSeparator,
            };

            var saved = (CurrencyDto)vm.OnRequestSave();

            Assert.Equal(expectedDecimal, saved.DecimalSeparator);
            Assert.Equal(expectedGroup, saved.GroupSeparator);
        }

        [Fact]
        public void OnRequestSave_UntouchedCurrency_KeepsItsSeparators()
        {
            var vm = new CurrencyDialogVM(new CurrencyDto(new Currency { Decimals = 2, DecimalSeparator = "','", GroupSeparator = "' '" }));

            var saved = (CurrencyDto)vm.OnRequestSave();

            Assert.Equal("','", saved.DecimalSeparator);
            Assert.Equal("' '", saved.GroupSeparator);
        }

        [Theory]
        [InlineData("COMMA", "SPACE", DecimalSeparator.COMMA, GroupSeparator.SPACE)]
        [InlineData("PERIOD", "NONE", DecimalSeparator.PERIOD, GroupSeparator.NONE)]
        public void Constructor_SeparatorsStoredAsEnumNames_AreRecognized(string storedDecimal, string storedGroup, DecimalSeparator expectedDecimal, GroupSeparator expectedGroup)
        {
            // currencies saved by an older build carry the member names; opening and saving them has to repair them
            var vm = new CurrencyDialogVM(new CurrencyDto(new Currency { Decimals = 2, DecimalSeparator = storedDecimal, GroupSeparator = storedGroup }));

            Assert.Equal(expectedDecimal, vm.SelectedDecimalSeparator);
            Assert.Equal(expectedGroup, vm.SelectedGroupSeparator);
        }
    }
}
