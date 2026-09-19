using System.Collections.Generic;
using System.Threading.Tasks;
using Financisto.DataAccess.Data;

namespace Financisto.Adapter
{
    public interface IEntityReader
    {
        Task<(IEnumerable<Entity> Entities, BackupVersion BackupVersion, Dictionary<string, List<string>> EntityColumnsOrder)> ParseBackupFileAsync(string fileName);
    }
}
