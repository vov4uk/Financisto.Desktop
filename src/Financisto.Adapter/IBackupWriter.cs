using System.Collections.Generic;
using System.Threading.Tasks;
using Financisto.DataAccess.Data;

namespace Financisto.Adapter
{
    public interface IBackupWriter
    {
        Task GenerateBackupAsync(
            IEnumerable<Entity> entities,
            string fileName,
            BackupVersion backupVersion,
            Dictionary<string, List<string>> entityColumnsOrder);
    }
}
