using System.Diagnostics.CodeAnalysis;

namespace Financisto.Common.Model
{
    [ExcludeFromCodeCoverage]
    public class ExchangeRateModel : BaseModel
    {
        public int FromCurrencyId { get; set; }

        public int ToCurrencyId { get; set; }

        public long Date { get; set; }

        public double Rate { get; set; }

        public CurrencyModel FromCurrency { get; set; }

        public CurrencyModel ToCurrency { get; set; }
    }
}
