using System.ComponentModel;
using Financisto.Common.Attribute;
using Financisto.Common.Converters;

namespace Financisto.Common.Entities
{
    [TypeConverter(typeof(EnumDescriptionTypeConverter))]
    public enum ReportStructureSaldoRange
    {
        [LocalizedDescription("reports_saldo_range_current_year")]
        CurrentYear,
        [LocalizedDescription("reports_saldo_range_last_6_months")]
        Last6Months,
        [LocalizedDescription("reports_saldo_range_last_12_months")]
        Last12Months,
        [LocalizedDescription("reports_saldo_range_last_2_years")]
        Last2Years,
        [LocalizedDescription("reports_saldo_range_last_24_months")]
        Last24Months,
        [LocalizedDescription("reports_saldo_range_all_periods")]
        AllPeriods
    }
}
