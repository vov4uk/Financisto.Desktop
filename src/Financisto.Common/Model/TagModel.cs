using System.ComponentModel.DataAnnotations.Schema;

namespace Financisto.Common.Model
{
    public class TagModel: TagBaseModel
    {
        [Column("sort_order")]
        public int SortOrder { get; set; }
    }
}
