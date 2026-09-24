using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace Financisto.DataAccess.Data
{
    [DebuggerDisplay("{Title}")]
    [Table(Backup.TAG_TABLE)]
    public class Tag : TagBase
    {
    }
}
