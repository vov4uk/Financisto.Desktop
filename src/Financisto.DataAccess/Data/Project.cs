using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace Financisto.DataAccess.Data
{
    [DebuggerDisplay("{Title}")]
    [Table(Backup.PROJECT_TABLE)]
    public class Project : TagBase
    {
    }
}
