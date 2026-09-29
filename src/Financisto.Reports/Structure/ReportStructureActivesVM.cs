using System;
using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Attribute;
using Financisto.Common.Localization;
using Financisto.Common.Utils;
using Financisto.DataAccess.Abstractions;

namespace Financisto.Reports
{
    [Header("reports_assets_structure")]
    public class ReportStructureActivesVM : BaseReportVM<ReportStructureActivesModel>
    {
        private static readonly string BaseSqlText = @" /* ReportStructureActivesVM */
SELECT account_title,
       account_id,
       account_is_active,
       is_include_into_totals,
       sort_order,
       balance,
       symbol,
       " + ExchangeRateSql.Convert("balance", "currency_id", ExchangeRateSql.HomeCurrencyId, "{0}") + @" AS balance_default_crr,
       " + ExchangeRateSql.Convert("balance", "currency_id", ExchangeRateSql.UsdCurrencyId, "{0}") + @" AS balance_usd,
       default_crr_symbol,
       date
FROM   (SELECT a.title AS account_title,
               a.is_active AS account_is_active,
               a.is_include_into_totals,
               a.sort_order,
               a._id AS account_id,
               Row_number() OVER ( partition BY a._id
                                   ORDER BY Date(t.datetime / 1000, 'unixepoch') DESC, t.datetime DESC, r.transaction_id DESC
               ) AS RowNum,
               r.balance / 100.0 AS balance,
               c._id AS currency_id,
               c.symbol,
               (SELECT symbol FROM   currency WHERE  is_default = 1) AS default_crr_symbol,
               Date(t.datetime / 1000, 'unixepoch') AS date
        FROM running_balance r
             INNER JOIN account a ON a._id = r.account_id
             INNER JOIN currency c ON a.currency_id = c._id
             INNER JOIN transactions t ON t._id = r.transaction_id
         WHERE t.datetime <= {0}
        ORDER BY a._id, r.datetime DESC ) rep
WHERE RowNum = 1
ORDER BY account_is_active DESC, sort_order ASC";

        public ReportStructureActivesVM(IFinancistoDatabase financistoDatabase) : base(financistoDatabase)
        {
            DateFilter = DateTime.Now;
        }

        protected override string GetSql()
        {
            if (!DateFilter.HasValue)
            {
                DialogService.ShowMessage(LocalizationService.Instance.please_select_date);
                return string.Empty;
            }

            return string.Format(BaseSqlText, GetStandartTrnFilter());
        }

        protected override ReportChart GetChart(List<ReportStructureActivesModel> list)
        {
            var included = list.Where(x => x.AccountIsIncludeInTotals == 1 && x.DefaultCurrencyBalance > 0.0).ToList();
            var total = included.Sum(x => x.DefaultCurrencyBalance ?? 0.0);

            // accounts below 1% of the total are merged into a single "others" slice
            var others = included.Where(x => x.DefaultCurrencyBalance != null && (x.DefaultCurrencyBalance / total) < 0.01).ToList();

            var slices = included
                .Where(x => !others.Contains(x))
                .Select(x => (x.Title, x.DefaultCurrencyBalance ?? 0))
                .ToList();

            if (others.Count > 0)
            {
                slices.Add((LocalizationService.Instance.others, others.Sum(x => x.DefaultCurrencyBalance) ?? 0));
            }

            return ReportCharts.Pie(slices);
        }
    }
}
