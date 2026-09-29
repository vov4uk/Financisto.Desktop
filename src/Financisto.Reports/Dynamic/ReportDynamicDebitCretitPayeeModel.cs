using Financisto.Common.Model;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financisto.Reports
{
    public class ReportDynamicDebitCretitPayeeModel : BaseModel
    {
        [DisplayName("year")]
        [Column("date_year")]
        public int Year { get; protected set; }

        [DisplayName("month")]
        [Column("date_month")]
        public int Month { get; protected set; }

        public string PeriodDesr => string.Format("{0} {1}", Month, Year);

        [Column("total")]
        [DisplayName("total_label")]
        public double? Total { get; protected set; }
    }
}
