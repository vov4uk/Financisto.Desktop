using static Financisto.BankHelpers.BankParsing;

namespace Financisto.BankHelpers.ABank
{
    /// <summary>What the Excel and the PDF helpers of A Bank have in common.</summary>
    internal static class ABankInfo
    {
        public static string Title => BankHelperBase.Localized("A Bank", ("uk", "А Банк"));

        public static BankTransaction ToBankTransaction(AbankRow item)
        {
            var operationCurrency = item.OperationCurrency;
            var operationAmount = GetDouble(item.OperationAmount);
            var cardCurrencyAmount = GetDouble(item.CardCurrencyAmount);

            return new BankTransaction
            {
                Balance = GetDouble(item.Balance),
                Cashback = GetDouble(item.Cashback),
                Commission = GetDouble(item.Commision),
                ExchangeRate = GetDouble(item.ExchangeRate),
                OperationCurrency = operationAmount != cardCurrencyAmount ? operationCurrency : null,
                OperationAmount = operationAmount,
                CardCurrencyAmount = cardCurrencyAmount,
                MCC = item.MCC,
                Description = item.Details,
                Date = ParseDateTime(item.Date)
            };
        }
    }
}
