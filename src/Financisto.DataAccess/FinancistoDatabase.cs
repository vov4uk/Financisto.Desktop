using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.DataAccess.DataBase.Scripts;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NLog;

namespace Financisto.DataAccess
{
    [ExcludeFromCodeCoverage]
    public class FinancistoDatabase : IFinancistoDatabase
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly ConcurrentDictionary<Type, (PropertyInfo Property, string ColumnName)[]> PropertyCache = new();
        private readonly DbConnection _connection;
        private bool isDisposed;

        internal FinancistoDatabase()
            : this(
                new DbContextOptionsBuilder<FinancistoDataContext>()
                    .UseSqlite(CreateInMemoryDatabase())
                    .EnableSensitiveDataLogging(true)
                    .Options)
        {
            _connection = RelationalOptionsExtension.Extract(ContextOptions).Connection!;
        }

        protected FinancistoDatabase(DbContextOptions<FinancistoDataContext> contextOptions)
        {
            ContextOptions = contextOptions;
        }

        private static DbConnection CreateInMemoryDatabase()
        {
            var connection = new SqliteConnection("Filename=:memory:");

            connection.Open();

            return connection;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected DbContextOptions<FinancistoDataContext> ContextOptions { get; }

        internal async Task SeedAsync()
        {
            using var context = new FinancistoDataContext(ContextOptions);

            ResourceSet create = SQL_create_files.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true)!;
            foreach (DictionaryEntry entry in create)
            {
                await context.Database.ExecuteSqlRawAsync(Convert.ToString(entry.Value)!);
            }

            var alter = SQL_alter_files.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true)!
                .Cast<DictionaryEntry>()
                .Select(entry => new KeyValuePair<string, string>(Convert.ToString(entry.Key)!, Convert.ToString(entry.Value)!))
                .OrderBy(x => x.Key)
                .ToList();
            foreach (var entry in alter)
            {
                await context.Database.ExecuteSqlRawAsync(entry.Value);
            }

            var view = SQL_views_files.ResourceManager.GetResourceSet(CultureInfo.CurrentUICulture, true, true)!
                .Cast<DictionaryEntry>()
                .Select(entry => new KeyValuePair<string, string>(Convert.ToString(entry.Key)!, Convert.ToString(entry.Value)!))
                .OrderBy(x => x.Key)
                .ToList();
            foreach (var entry in view)
            {
                await context.Database.ExecuteSqlRawAsync(entry.Value);
            }

            await context.SaveChangesAsync();
        }

        public async Task ImportEntitiesAsync(IEnumerable<Entity> entities)
        {
            await SeedAsync();

            await using (var context = new FinancistoDataContext(ContextOptions))
            {
                await context.AddRangeAsync(entities.OfType<IIdentity>().Where(x => x.Id > 0));
                await context.SaveChangesAsync();

                // Same as Android's DatabaseImport: the restore scripts fix up the rows just imported.
                foreach (var item in Backup.RESTORE_SCRIPTS)
                {
                    var sql = SQL_alter_files.ResourceManager.GetString(item);
                    await context.Database.ExecuteSqlRawAsync(sql!);
                }

                // Same as Android's IntegrityFix (run by FullDatabaseImport): older backups have split parts without
                // parent_account_id, which the running balance needs to tell the parent account's side of a split transfer.
                await context.Database.ExecuteSqlRawAsync(
                    "UPDATE transactions SET parent_account_id = (SELECT p.from_account_id FROM transactions AS p WHERE p._id = transactions.parent_id) " +
                    "WHERE parent_id != 0 AND parent_account_id = 0 AND EXISTS (SELECT 1 FROM transactions AS p WHERE p._id = transactions.parent_id)");
            }

            var accounts = entities.OfType<Account>().ToList();
            foreach (var item in accounts)
            {
                await RebuildAccountBalanceAsync(item.Id);
            }
        }

        public async Task RebuildAccountBalanceAsync(int accountId)
        {
            if (accountId > 0)
            {
                await using (var context = new FinancistoDataContext(ContextOptions))
                {
                    await context.Database.ExecuteSqlRawAsync("delete from running_balance where account_id=@p0", accountId);
                    await context.SaveChangesAsync();

                    // Same order as Android's rebuildRunningBalanceForAccount, so the last row by (datetime, transaction_id)
                    // holds the account's total, as GetLastRunningBalancesAsync expects.
                    var transactions = await context.BlotterTransactionsForAccountWithSplits.Where(x => x.FromAccountId == accountId)
                        .OrderBy(x => x.DateTime).ThenBy(x => x.Id).ToListAsync();
                    long balance = 0;

                    foreach (var transaction in transactions)
                    {
                        if (transaction.ParentId > 0 && transaction.ParentAccountId == accountId)
                        {
                            // Same as Android's rebuildRunningBalanceForAccount: the split parent already carries
                            // the amount for its own account, so only the other account's side of a split transfer
                            // counts here, whether it is the "to" side (outgoing) or the "from" side (incoming).
                            continue;
                        }
                        var toAccountId = transaction.ToAccountId;
                        if (toAccountId > 0 && toAccountId == transaction.FromAccountId)
                        {
                            // weird bug when a transfer is done from an account to the same account
                            continue;
                        }
                        balance += transaction.FromAmount;
                        context.RunningBalance.Add(new RunningBalance { Balance = balance, AccountId = accountId, TransactionId = transaction.Id, Datetime = transaction.DateTime });
                    }

                    var acc = context.Accounts.FirstOrDefault(x => x.Id == accountId);
                    var lastTransaction = transactions.LastOrDefault();
                    if (acc != null)
                    {
                        acc.TotalAmount = balance;
                        acc.LastTransactionDate = lastTransaction?.DateTime ?? 0;
                        acc.LastTransactionId = lastTransaction?.Id ?? 0;
                        context.Accounts.Update(acc);
                    }
                    await context.SaveChangesAsync();
                }
            }
        }

        public async Task DeleteAccountAsync(int accountId)
        {
            if (accountId <= 0)
            {
                return;
            }

            await using var context = new FinancistoDataContext(ContextOptions);
            await using var dbTransaction = await context.Database.BeginTransactionAsync();

            await BreakTransfersAsync(context, accountId, null);
            await context.Database.ExecuteSqlRawAsync("delete from transactions where from_account_id=@p0", accountId);
            await context.Database.ExecuteSqlRawAsync("delete from running_balance where account_id=@p0", accountId);
            await DetachOrphanSplitPartsAsync(context);
            await context.Database.ExecuteSqlRawAsync("delete from account where _id=@p0", accountId);

            await dbTransaction.CommitAsync();
        }

        public async Task PurgeAccountAsync(int accountId, long date, string previousPeriodPayeeTitle)
        {
            if (accountId <= 0)
            {
                return;
            }

            var dayEnd = AtDayEnd(date);
            await using (var context = new FinancistoDataContext(ContextOptions))
            {
                // The account's balance after its latest row up to the cut; incoming transfers have a row here too,
                // which Android's lookup in v_blotter by from_account_id would miss.
                var last = await context.RunningBalance
                    .Where(x => x.AccountId == accountId && x.Datetime <= dayEnd)
                    .OrderByDescending(x => x.Datetime).ThenByDescending(x => x.TransactionId)
                    .FirstOrDefaultAsync();
                if (last == null)
                {
                    return;
                }

                await using var dbTransaction = await context.Database.BeginTransactionAsync();

                var now = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
                var payee = await context.Payees.FirstOrDefaultAsync(x => x.Title == previousPeriodPayeeTitle);
                if (payee == null)
                {
                    payee = new Payee { Id = 0, Title = previousPeriodPayeeTitle, IsActive = true, UpdatedOn = now };
                    context.Payees.Add(payee);
                    await context.SaveChangesAsync();
                }

                await BreakTransfersAsync(context, accountId, dayEnd);
                await context.Database.ExecuteSqlRawAsync(
                    "delete from transactions where from_account_id=@p0 and datetime<=@p1 and is_template=0", accountId, dayEnd);
                await context.Database.ExecuteSqlRawAsync(
                    "delete from running_balance where account_id=@p0 and datetime<=@p1", accountId, dayEnd);
                await DetachOrphanSplitPartsAsync(context);

                context.Transactions.Add(new Transaction
                {
                    Id = 0,
                    FromAccountId = accountId,
                    DateTime = AtDayEnd(last.Datetime),
                    FromAmount = last.Balance,
                    PayeeId = payee.Id,
                    Status = "CL",
                    UpdatedOn = now,
                });
                await context.SaveChangesAsync();

                await dbTransaction.CommitAsync();
            }

            // Same total as before; this also rebuilds the running balance and the account's last transaction.
            await RebuildAccountBalanceAsync(accountId);
        }

        /// <summary>
        /// Android's UPDATE_ORPHAN_TRANSACTIONS_1/2: a transfer to the account becomes an expense of the other account, a transfer from
        /// it becomes an income of the other account. Limited to the transactions up to <paramref name="untilDate"/> when it is given.
        /// </summary>
        private static async Task BreakTransfersAsync(FinancistoDataContext context, int accountId, long? untilDate)
        {
            var dateFilter = untilDate == null ? string.Empty : " and datetime<=@p1 and is_template=0";
            var parameters = untilDate == null ? new object[] { accountId } : new object[] { accountId, untilDate.Value };

            await context.Database.ExecuteSqlRawAsync(
                "update transactions set to_account_id=0, to_amount=0 where to_account_id=@p0" + dateFilter, parameters);
            await context.Database.ExecuteSqlRawAsync(
                "update transactions set from_account_id=to_account_id, from_amount=to_amount, to_account_id=0, to_amount=0, " +
                "parent_id=0, parent_account_id=0 where from_account_id=@p0 and to_account_id>0" + dateFilter, parameters);
        }

        /// <summary>
        /// A split part that moved money into the deleted parent's account is stored on the other account, so it survives its parent;
        /// it becomes a plain transaction of that account.
        /// </summary>
        private static Task DetachOrphanSplitPartsAsync(FinancistoDataContext context) =>
            context.Database.ExecuteSqlRawAsync(
                "update transactions set parent_id=0, parent_account_id=0 where parent_id>0 and parent_id not in (select _id from transactions)");

        /// <summary>Android's <c>DateUtils.atDayEnd</c>: 23:59:59.999 of the local day.</summary>
        private static long AtDayEnd(long unixMilliseconds)
        {
            var local = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).ToLocalTime();
            var end = new DateTime(local.Year, local.Month, local.Day, 23, 59, 59, 999, DateTimeKind.Local);
            return new DateTimeOffset(end).ToUnixTimeMilliseconds();
        }

        public async Task<Dictionary<int, long>> GetLastRunningBalancesAsync()
        {
            // Android's getLastRunningBalanceForAccount ("order by datetime desc, transaction_id desc limit 1") for every account at once.
            // An account without transactions has no rows, i.e. a balance of 0.
            var rows = await ExecuteQuery<RunningBalance>(@"
SELECT account_id, transaction_id, datetime, balance
FROM   (SELECT *,
               ROW_NUMBER() OVER (PARTITION BY account_id ORDER BY datetime DESC, transaction_id DESC) AS row_num
        FROM   running_balance)
WHERE  row_num = 1");
            return rows.ToDictionary(x => x.AccountId, x => x.Balance);
        }

        public async Task AddTransactionsAsync(IEnumerable<Transaction> transactions)
        {
            using var uow = CreateUnitOfWork();
            await uow.GetRepository<Transaction>().AddRangeAsync(transactions);

            await uow.SaveChangesAsync();
        }

        public IUnitOfWork CreateUnitOfWork()
        {
            return new UnitOfWork<FinancistoDataContext>(new FinancistoDataContext(ContextOptions));
        }

        public async Task<T> GetOrCreateAsync<T>(int id)
            where T : class, IIdentity, new()
        {
            if (id != 0)
            {
                using var uow = CreateUnitOfWork();
                return await uow.GetRepository<T>().FindByAsync(x => x.Id == id);
            }
            return new T { Id = 0 };
        }

        public async Task<Transaction> GetOrCreateTransactionAsync(int id)
        {
            if (id != 0)
            {
                using var uow = CreateUnitOfWork();
                return await uow.GetRepository<Transaction>().FindByAsync(x => x.Id == id, x => x.FromAccount);
            }

            return new Transaction { DateTime = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds(), Id = 0, CategoryId = 0 };
        }

        public async Task<IEnumerable<Transaction>> GetSubTransactionsAsync(int id)
        {
            if (id != 0)
            {
                using var uow = CreateUnitOfWork();
                var subTransactions = await uow.GetRepository<Transaction>().
                    FindManyAsync(
                    x => x.ParentId == id,
                    o => o.OriginalCurrency,
                    c => c.Category);

                if (subTransactions == null)
                {
                    return Array.Empty<Transaction>();
                }
                return subTransactions;
            }

            return Array.Empty<Transaction>();
        }

        public async Task InsertOrUpdateAsync<T>(IEnumerable<T> entities)
            where T : Entity, IIdentity
        {
            using var uow = CreateUnitOfWork();
            var trRepo = uow.GetRepository<T>();
            foreach (var item in entities)
            {
                item.UpdatedOn = new DateTimeOffset(DateTime.Now).ToUnixTimeMilliseconds();
                if (item.Id == 0)
                {
                    await trRepo.AddAsync(item);
                }
                else
                {
                    await trRepo.UpdateAsync(item);
                }
            }
            await uow.SaveChangesAsync();
        }

        public async Task<List<T>> ExecuteQuery<T>(string query) where T : class, new()
        {
            await using var db = new FinancistoDataContext(ContextOptions);
            using var command = db.Database.GetDbConnection().CreateCommand();

            Logger.Info(query);
            command.CommandText = query;
            command.CommandType = CommandType.Text;

            await db.Database.OpenConnectionAsync();

            var mappings = PropertyCache.GetOrAdd(typeof(T), static t =>
                t.GetProperties()
                 .Select(p => (Property: p, Column: p.GetCustomAttribute<ColumnAttribute>()?.Name))
                 .Where(x => x.Column != null)
                 .Select(x => (x.Property, x.Column!))
                 .ToArray());

            await using var reader = await command.ExecuteReaderAsync();

            var columnOrdinals = Enumerable.Range(0, reader.FieldCount)
                .ToDictionary(i => reader.GetName(i), i => i);

            var ordinals = new int[mappings.Length];
            for (int i = 0; i < mappings.Length; i++)
            {
                if (!columnOrdinals.TryGetValue(mappings[i].ColumnName, out var ordinal))
                {
                    throw new InvalidCastException(string.Format("Class [{0}] have attribute of field [{1}] which not exist in reader", typeof(T), mappings[i].ColumnName));
                }
                ordinals[i] = ordinal;
            }

            var lst = new List<T>();
            while (await reader.ReadAsync())
            {
                var newObject = new T();
                for (int i = 0; i < mappings.Length; i++)
                {
                    var obj = reader.GetValue(ordinals[i]);
                    if (obj != DBNull.Value)
                    {
                        mappings[i].Property.SetValue(newObject, Unbox(obj, mappings[i].Property.PropertyType));
                    }
                }
                lst.Add(newObject);
            }

            return lst;
        }

        public async Task SaveAsFile(string dest)
        {
            await using (var db = new FinancistoDataContext(ContextOptions))
            using (var command = db.Database.GetDbConnection().CreateCommand())
            {
                var escapedDest = dest.Replace("'", "''");
                command.CommandText = $"VACUUM main INTO '{escapedDest}'";
                command.CommandType = CommandType.Text;
                Logger.Info("VACUUM main INTO @path (path={0})", dest);

                await db.Database.OpenConnectionAsync();

                await command.ExecuteNonQueryAsync();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.isDisposed)
            {
                return;
            }

            if (disposing)
            {
                _connection.Dispose();
            }

            this.isDisposed = true;
        }

        static object Unbox(object x, Type t)
        {
            var underlyingType = Nullable.GetUnderlyingType(t);
            return Convert.ChangeType(x, underlyingType ?? t);
        }
    }
}
