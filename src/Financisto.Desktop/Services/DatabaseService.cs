using Financisto.Adapter;
using Financisto.Common.Entities;
using Financisto.DataAccess;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.DataAccess.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Financisto.Desktop.Services
{
    public class DatabaseService
    {
        private readonly IFinancistoDatabaseFactory _dbFactory = new FinancistoDatabaseFactory();
        private readonly IEntityReader _entityReader = new EntityReader();
        private readonly IBackupWriter _backupWriter = new BackupWriter();
        private readonly List<Entity> _keyLessEntities = new();

        private BackupVersion _backupVersion;
        private Dictionary<string, List<string>> _entityColumnsOrder;

        public IFinancistoDatabase CurrentDatabase { get; private set; }

        public async Task<int> OpenBackupAsync(string backupPath)
        {
            var (entities, backupVersion, entityColumnsOrder) = await _entityReader.ParseBackupFileAsync(backupPath);
            var materialized = entities as IReadOnlyCollection<Entity> ?? entities.ToList();

            _backupVersion = backupVersion;
            _entityColumnsOrder = entityColumnsOrder;

            CurrentDatabase?.Dispose();
            CurrentDatabase = _dbFactory.CreateDatabase();
            await CurrentDatabase.ImportEntitiesAsync(materialized);

            // These entity types have no primary key, so ImportEntitiesAsync (which only
            // imports IIdentity rows) never persists them into the database. Keep them as
            // parsed so SaveBackupAsync can still round-trip them.
            _keyLessEntities.Clear();
            _keyLessEntities.AddRange(materialized.OfType<CCardClosingDate>());
            _keyLessEntities.AddRange(materialized.OfType<CategoryAttribute>());
            _keyLessEntities.AddRange(materialized.OfType<TransactionAttribute>());

            DbManual.ResetAllDatabaseManuals();
            await DbManual.SetupAsync(CurrentDatabase);

            return materialized.Count;
        }

        public async Task SaveBackupAsync(string backupPath)
        {
            if (CurrentDatabase == null)
            {
                return;
            }

            List<Entity> itemsToBackup = new(_keyLessEntities);
            using (IUnitOfWork uow = CurrentDatabase.CreateUnitOfWork())
            {
                itemsToBackup.AddRange(await uow.GetAllAsync<Budget>());
                itemsToBackup.AddRange(await uow.GetAllAsync<TransactionAttribute>());
                itemsToBackup.AddRange(await uow.GetAllAsync<CurrencyExchangeRate>());
                itemsToBackup.AddRange(await uow.GetAllAsync<Currency>());
                itemsToBackup.AddRange((await uow.GetAllAsync<Location>()).Where(x => x.Id > 0));
                itemsToBackup.AddRange(await uow.GetAllAsync<Payee>());
                itemsToBackup.AddRange((await uow.GetAllAsync<Project>()).Where(x => x.Id > 0));
                itemsToBackup.AddRange(await uow.GetAllAsync<Transaction>());
                itemsToBackup.AddRange((await uow.GetAllAsync<Account>()).OrderBy(x => x.Id));
                itemsToBackup.AddRange((await uow.GetAllAsync<AttributeDefinition>()).Where(x => x.Id > 0));
                itemsToBackup.AddRange(await uow.GetAllAsync<CategoryAttribute>());
                itemsToBackup.AddRange(await uow.GetAllAsync<CCardClosingDate>());
                itemsToBackup.AddRange(await uow.GetAllAsync<SmsTemplate>());
                itemsToBackup.AddRange((await uow.GetAllAsync<Category>()).Where(x => x.Id > 0));
            }

            await _backupWriter.GenerateBackupAsync(itemsToBackup, backupPath, _backupVersion, _entityColumnsOrder);
        }

        public Task SaveAsDbAsync(string dbPath)
        {
            return CurrentDatabase?.SaveAsFile(dbPath) ?? Task.CompletedTask;
        }
    }
}
