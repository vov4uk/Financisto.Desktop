using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Financisto.Common.Model
{
    [ExcludeFromCodeCoverage]
    public class LocationModel : TagModel
    {
        [Column("resolved_address")]
        public string Address { get; set; }
    }
}
