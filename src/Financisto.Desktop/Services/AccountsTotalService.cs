using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Models;

namespace Financisto.Desktop.Services
{
    public record AccountsTotals(
        CurrencyModel HomeCurrency,
        LatestExchangeRates Rates,
        IReadOnlyList<Total> TotalsPerCurrency,
        Total TotalInHomeCurrency);

    /// <summary>
    /// Port of the account totals part of tw.tib.financisto.db.DatabaseAdapter
    /// (getAccountsTotal, getAccountsTotalWithFilter, getLatestRates, getHomeCurrency).
    /// </summary>
    public class AccountsTotalService
    {
        // DatabaseHelper.ExchangeRateColumns.LATEST_RATE_PROJECTION grouped by LATEST_RATE_GROUP_BY:
        // SQLite takes the bare "rate" column from the row that holds max(rate_date)
        private const string LatestRatesSqlText = @" /* AccountsTotalService */
SELECT from_currency_id,
       to_currency_id,
       Max(rate_date) AS rate_date,
       rate
FROM   v_exchange_rate
GROUP  BY from_currency_id,
          to_currency_id";

        private readonly IFinancistoDatabase db;

        public AccountsTotalService(IFinancistoDatabase db)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<AccountsTotals> GetAccountsTotalsAsync()
        {
            List<AccountModel> accounts;
            CurrencyModel homeCurrency;
            using (var uow = db.CreateUnitOfWork())
            {
                accounts = await uow.GetRepository<Account>().FindManyAndProjectAsync(
                    predicate: x => true,
                    projection: acc => new AccountModel(acc),
                    includes: x => x.Currency);

                var defaultCurrency = await uow.GetRepository<Currency>().FindByAsync(x => x.IsDefault);
                homeCurrency = new CurrencyModel(defaultCurrency ?? Currency.EMPTY);
            }

            var rates = new LatestExchangeRates(homeCurrency.Id ?? 0, await db.ExecuteQuery<ExchangeRate>(LatestRatesSqlText));

            return new AccountsTotals(
                homeCurrency,
                rates,
                GetAccountsTotalWithFilter(accounts),
                GetAccountsTotal(accounts, homeCurrency, rates));
        }

        /// <summary>
        /// Calculates total in every currency for all accounts
        /// </summary>
        public static Total[] GetAccountsTotalWithFilter(IEnumerable<AccountModel> accounts)
        {
            var totalsMap = new Dictionary<int, Total>();
            foreach (var account in accounts.Where(ShouldIncludeIntoTotals))
            {
                if (!totalsMap.TryGetValue(account.CurrencyId, out var total))
                {
                    total = new Total(account.Currency);
                    totalsMap[account.CurrencyId] = total;
                }
                total.Balance += account.TotalAmount;
            }
            return totalsMap.Values.ToArray();
        }

        /// <summary>
        /// Calculates total in home currency for all accounts
        /// </summary>
        public static Total GetAccountsTotal(IEnumerable<AccountModel> accounts, CurrencyModel homeCurrency, LatestExchangeRates rates)
        {
            decimal total = 0;
            foreach (var account in accounts.Where(ShouldIncludeIntoTotals))
            {
                if (account.CurrencyId == homeCurrency.Id)
                {
                    total += account.TotalAmount;
                }
                else
                {
                    var rate = rates.GetRate(account.CurrencyId, homeCurrency.Id ?? 0);
                    if (rate == ExchangeRate.NA)
                    {
                        return new Total(homeCurrency, TotalError.LastRateError(account.Currency));
                    }
                    total += (decimal)(rate.Rate * account.TotalAmount);
                }
            }
            // BigDecimal.longValue() truncates, so does the decimal -> long cast
            return new Total(homeCurrency) { Balance = (long)total };
        }

        private static bool ShouldIncludeIntoTotals(AccountModel account) => account.IsActive && account.IsIncludeIntoTotals;
    }
}
