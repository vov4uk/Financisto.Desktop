using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financisto.Desktop.Services
{
    // port of tw.tib.financisto.rates.ExchangeRate
    public class ExchangeRate
    {
        public static readonly ExchangeRate ONE = new() { Rate = 1.0 };
        public static readonly ExchangeRate NA = new();

        [Column("from_currency_id")]
        public int FromCurrencyId { get; set; }

        [Column("to_currency_id")]
        public int ToCurrencyId { get; set; }

        [Column("rate_date")]
        public long Date { get; set; }

        [Column("rate")]
        public double Rate { get; set; }

        public ExchangeRate Flip() => new()
        {
            FromCurrencyId = ToCurrencyId,
            ToCurrencyId = FromCurrencyId,
            Date = Date,
            Rate = Rate == 0 ? 0 : 1.0 / Rate,
        };
    }

    /// <summary>
    /// Port of tw.tib.financisto.rates.LatestExchangeRates: the latest known rate per currency pair,
    /// with fallbacks to the inverse rate and to a cross rate via the home currency.
    /// </summary>
    /// <remarks>
    /// The Android fallback via currency.trading_currency_id is not ported yet; the column is
    /// imported (Currency.TradingCurrencyId) but not used here.
    /// </remarks>
    public class LatestExchangeRates
    {
        private readonly Dictionary<int, Dictionary<int, ExchangeRate>> rates = new();
        private readonly int homeCurrencyId;

        public LatestExchangeRates(int homeCurrencyId, IEnumerable<ExchangeRate> latestRates)
        {
            this.homeCurrencyId = homeCurrencyId;
            foreach (var rate in latestRates)
            {
                AddRate(rate);
            }
        }

        public void AddRate(ExchangeRate r)
        {
            GetMapFor(r.FromCurrencyId)[r.ToCurrencyId] = r;
        }

        public ExchangeRate GetRate(int fromCurrencyId, int toCurrencyId)
        {
            return GetRate(fromCurrencyId, toCurrencyId, new HashSet<(int, int)>());
        }

        private ExchangeRate GetRate(int fromCurrencyId, int toCurrencyId, HashSet<(int, int)> usedCurrencies)
        {
            if (fromCurrencyId == toCurrencyId)
            {
                return ExchangeRate.ONE;
            }

            var rateMap = GetMapFor(fromCurrencyId);
            if (!usedCurrencies.Add((fromCurrencyId, toCurrencyId)))
            {
                // negative cache
                rateMap[toCurrencyId] = ExchangeRate.NA;
                return ExchangeRate.NA;
            }

            if (rateMap.TryGetValue(toCurrencyId, out var rate))
            {
                return rate;
            }

            // estimate from inverse exchange
            if (GetMapFor(toCurrencyId).TryGetValue(fromCurrencyId, out var inverseRate) && inverseRate != ExchangeRate.NA)
            {
                var inverse = inverseRate.Flip();
                rateMap[toCurrencyId] = inverse;
                return inverse;
            }

            // estimate from exchange via home currency
            if (homeCurrencyId != 0 && fromCurrencyId != homeCurrencyId && toCurrencyId != homeCurrencyId)
            {
                var e1 = GetRate(fromCurrencyId, homeCurrencyId, usedCurrencies);
                if (e1 != ExchangeRate.NA)
                {
                    var e2 = GetRate(homeCurrencyId, toCurrencyId, usedCurrencies);
                    if (e2 != ExchangeRate.NA)
                    {
                        return CombineRate(rateMap, fromCurrencyId, toCurrencyId, e1, e2);
                    }
                }
            }

            // negative cache
            rateMap[toCurrencyId] = ExchangeRate.NA;
            return ExchangeRate.NA;
        }

        private static ExchangeRate CombineRate(Dictionary<int, ExchangeRate> rateMap, int fromCurrencyId, int toCurrencyId,
            ExchangeRate e1, ExchangeRate e2)
        {
            var rate = new ExchangeRate
            {
                FromCurrencyId = fromCurrencyId,
                ToCurrencyId = toCurrencyId,
                Rate = e1.Rate * e2.Rate,
            };
            rateMap[toCurrencyId] = rate;
            return rate;
        }

        private Dictionary<int, ExchangeRate> GetMapFor(int fromCurrencyId)
        {
            if (!rates.TryGetValue(fromCurrencyId, out var map))
            {
                map = new Dictionary<int, ExchangeRate>();
                rates[fromCurrencyId] = map;
            }
            return map;
        }
    }
}
