using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Financisto.Common.Model
{
    [ExcludeFromCodeCoverage]
    public class Years : BaseModel
    {
        [Column("year")]
        public int? Year { get; set; }
    }
}