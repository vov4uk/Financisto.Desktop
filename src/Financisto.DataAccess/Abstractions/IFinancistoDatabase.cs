using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Financisto.DataAccess.Data;

namespace Financisto.DataAccess.Abstractions
{
    public interface IFinancistoDatabase : IUnitOfWorkFactory, IDisposable
    {
        Task ImportEntitiesAsync(IEnumerable<Entity> entities);

        Task RebuildAccountBalanceAsync(int accountId);

        /// <summary>Account id → current balance, the last running balance like Android's getLastRunningBalanceForAccount.</summary>
        Task<Dictionary<int, long>> GetLastRunningBalancesAsync();

        /// <summary>
        /// Android's <c>deleteAccount</c>: the account and its transactions go; a transfer to or from it stays in the other account
        /// as a plain transaction, so the other account's balance doesn't change.
        /// </summary>
        Task DeleteAccountAsync(int accountId);

        /// <summary>
        /// Android's <c>purgeAccountAtDate</c>: deletes the account's transactions up to and including the day of <paramref name="date"/>
        /// (Unix milliseconds) and replaces them with one "previous period" transaction that carries the balance at that point, so the
        /// account total doesn't change. Does nothing when the account has no transaction that old.
        /// </summary>
        /// <param name="previousPeriodPayeeTitle">Title of the payee of that transaction (found, or created).</param>
        Task PurgeAccountAsync(int accountId, long date, string previousPeriodPayeeTitle);

        Task AddTransactionsAsync(IEnumerable<Transaction> transactions);

        Task<T> GetOrCreateAsync<T>(int id)
            where T : class, IIdentity, new();

        Task<List<T>> ExecuteQuery<T>(string query)
            where T : class, new();

        Task<Transaction> GetOrCreateTransactionAsync(int id);

        Task<IEnumerable<Transaction>> GetSubTransactionsAsync(int id);

        Task InsertOrUpdateAsync<T>(IEnumerable<T> entities)
            where T : Entity, IIdentity;

        Task SaveAsFile(string dest);
    }
}
