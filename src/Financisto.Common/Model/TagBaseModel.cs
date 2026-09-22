using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;

namespace Financisto.Common.Model
{
    [ExcludeFromCodeCoverage]
    public class TagBaseModel : BaseModel, IActive
    {
        [Column("_id")]
        public int? Id { get; set; }

        [Column("title")]
        public string Title { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        override public string ToString() => Title;
    }
}
