using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Financisto.Common.Model
{
    [ExcludeFromCodeCoverage]
    public class YearMonths : BaseModel
    {
        [Column("year")]
        public int? Year { get; set; }

        [Column("month")]
        public int? Month { get; set; }

        public string Name => string.Format("{0} {1}",
            Month == null ?
                          string.Empty :
                          CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(Month ?? 0),
            Year);
    }
}