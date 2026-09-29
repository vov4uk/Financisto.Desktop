namespace Financisto.Common.Utils
{
    /// <summary>
    /// SQL for converting an amount between currencies with the rates stored in <c>currency_exchange_rate</c>, as of a
    /// given time. Shared by the reports and the dashboard, so both convert the same way.
    /// </summary>
    /// <remarks>
    /// The rate of a pair is the latest one on or before the time (the earliest one when the time is before all of
    /// them). When the pair has no rate, the same fallbacks as <c>LatestExchangeRates</c> (the port of Android's) apply:
    /// the inverse of the opposite pair, then a conversion through the home currency (each leg direct or inverse).
    /// When none of them exists the result is <c>NULL</c>, so the caller decides what an unknown amount counts as.
    /// </remarks>
    public static class ExchangeRateSql
    {
        /// <summary>The id of the home (default) currency.</summary>
        public const string HomeCurrencyId = "(SELECT _id FROM currency WHERE is_default = 1)";

        /// <summary>The id of the US dollar.</summary>
        public const string UsdCurrencyId = "(SELECT _id FROM currency WHERE name = 'USD')";

        /// <summary>
        /// The <paramref name="amount"/> converted from one currency to another as of <paramref name="atMs"/>, rounded
        /// to whole units when it is really converted; an amount that is already in the target currency is kept as it is.
        /// </summary>
        /// <param name="amount">SQL expression of the amount.</param>
        /// <param name="fromCurrencyId">SQL expression of the currency of the amount.</param>
        /// <param name="toCurrencyId">SQL expression of the currency to convert to, e.g. <see cref="HomeCurrencyId"/>.</param>
        /// <param name="atMs">SQL expression of the time, in unix milliseconds.</param>
        public static string Convert(string amount, string fromCurrencyId, string toCurrencyId, string atMs) =>
            $"CASE WHEN {fromCurrencyId} = {toCurrencyId} THEN {amount} " +
            $"ELSE Round(({amount}) * {Rate(fromCurrencyId, toCurrencyId, atMs)}, 0) END";

        /// <summary>
        /// The rate that converts an amount of <paramref name="fromCurrencyId"/> to <paramref name="toCurrencyId"/>
        /// as of <paramref name="atMs"/>, or <c>NULL</c> when it can't be found. The parameters are as in <see cref="Convert"/>.
        /// </summary>
        public static string Rate(string fromCurrencyId, string toCurrencyId, string atMs) =>
            $"COALESCE({Leg(fromCurrencyId, toCurrencyId, atMs)}, " +
            $"({Leg(fromCurrencyId, HomeCurrencyId, atMs)}) * ({Leg(HomeCurrencyId, toCurrencyId, atMs)}))";

        // one step of a conversion: the rate of the pair, or the inverse of the opposite pair
        private static string Leg(string from, string to, string atMs) =>
            $"CASE WHEN {from} = {to} THEN 1.0 ELSE COALESCE({Stored(from, to, atMs)}, 1.0 / {Stored(to, from, atMs)}) END";

        // the stored rate of a pair: the latest on or before the time, else the earliest
        private static string Stored(string from, string to, string atMs) =>
            $"(SELECT rate FROM currency_exchange_rate WHERE from_currency_id = {from} AND to_currency_id = {to} " +
            $"ORDER BY (rate_date <= {atMs}) DESC, CASE WHEN rate_date <= {atMs} THEN rate_date END DESC, rate_date ASC LIMIT 1)";
    }
}
