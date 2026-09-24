using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace Financisto.DataAccess.Data
{
    [DebuggerDisplay("{Title}")]
    [Table(Backup.PAYEE_TABLE)]
    public class Payee : TagBase, IHasAliases
    {
        [Column("last_category_id")]
        public long LastCategoryId { get; set; }

        // Kept in the backup's escaped form: aliases joined by the two characters \n.
        [Column(Backup.AliasesColumn)]
        public string Aliases { get; set; }
    }
}
