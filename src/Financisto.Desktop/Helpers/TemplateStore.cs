using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.DataAccess.View;
using Financisto.Desktop.ViewModels.Pages;

namespace Financisto.Desktop.Helpers
{
    /// <summary>
    /// Templates are transactions with <c>is_template = 1</c> (a split's parts carry the same flag), like Android's: the blotter views leave
    /// them out, so no balance depends on them. This reads them, turns one into a new transaction and copies a transaction into one.
    /// Showing and saving a template in a dialog is <see cref="TransactionEditor"/>'s job.
    /// </summary>
    internal sealed class TemplateStore
    {
        public const int TemplateFlag = 1;

        private readonly IFinancistoDatabase db;

        public TemplateStore(IFinancistoDatabase db)
        {
            this.db = db;
        }

        /// <summary>A template is a transfer (Android's <c>isTransfer</c>) when it moves money between two accounts and has no category.</summary>
        public static bool IsTransfer(Transaction transaction) => transaction.ToAccountId > 0 && transaction.CategoryId == 0 && transaction.FromAccountId > 0;

        /// <summary>The templates (never their parts), newest first like Android's default order.</summary>
        public async Task<List<BlotterModel>> GetTemplatesAsync()
        {
            var rows = await BlotterPageVM.QueryAsync<AllTransactions>(db, x => x.IsTemplate == TemplateFlag && x.ParentId == 0);
            return rows.OrderByDescending(x => x.Datetime).ThenByDescending(x => x.Id).ToList();
        }

        /// <summary>
        /// Android's <c>duplicateTransaction(templateId, multiplier)</c> without writing anything: the template as a new transaction dated now, its amounts
        /// times <paramref name="multiplier"/>, ready for the transaction or transfer dialog. Null when the template is gone.
        /// </summary>
        public async Task<(Transaction Transaction, List<Transaction> SubTransactions)?> CreateFromTemplateAsync(int templateId, int multiplier)
        {
            // id 0 would be a new, empty transaction
            var transaction = templateId > 0 ? await db.GetOrCreateTransactionAsync(templateId) : null;
            if (transaction == null)
            {
                return null;
            }

            var parts = (await db.GetSubTransactionsAsync(templateId)).ToList();
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            transaction.Id = 0;
            transaction.IsTemplate = 0;
            transaction.TemplateName = null;
            transaction.Recurrence = null;
            transaction.NotificationOptions = null;
            transaction.RemoteKey = null;
            transaction.DateTime = now;
            Multiply(transaction, multiplier);

            foreach (var part in parts)
            {
                // The copy gets its own parts; keeping the ids would re-parent the template's.
                part.Id = 0;
                part.IsTemplate = 0;
                part.RemoteKey = null;
                Multiply(part, multiplier);
            }

            return (transaction, parts);
        }

        /// <summary>
        /// Android's "Save as template" (<c>duplicateTransactionAsTemplate</c>): a copy of the transaction, with its split parts, that is a template.
        /// Like Android it has no name yet. A part of a split stands for its parent. Returns the template's id.
        /// </summary>
        public async Task<int> SaveAsTemplateAsync(int transactionId)
        {
            var transaction = transactionId > 0 ? await db.GetOrCreateTransactionAsync(transactionId) : null;
            if (transaction == null)
            {
                return 0;
            }

            if (transaction.ParentId > 0)
            {
                transactionId = transaction.ParentId;
                transaction = await db.GetOrCreateTransactionAsync(transactionId);
            }

            var parts = (await db.GetSubTransactionsAsync(transactionId)).ToList();
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            transaction.Id = 0;
            transaction.IsTemplate = TemplateFlag;
            transaction.RemoteKey = null;
            transaction.DateTime = now;
            transaction.LastRecurrence = now;
            DetachNavigations(transaction);

            foreach (var part in parts)
            {
                part.Id = 0;
                part.IsTemplate = TemplateFlag;
                part.RemoteKey = null;
                DetachNavigations(part);
                part.ParentId = 0;
                part.Parent = transaction;
            }

            await db.InsertOrUpdateAsync(parts.Prepend(transaction).ToList());
            return transaction.Id;
        }

        /// <summary>Deletes a template together with its parts.</summary>
        public async Task DeleteTemplateAsync(int templateId)
        {
            // id 0 would match every top-level row through parent_id below.
            if (templateId <= 0)
            {
                return;
            }

            using var uow = db.CreateUnitOfWork();
            await uow.GetRepository<Transaction>().DeleteAsync(x => x.IsTemplate == TemplateFlag && (x.Id == templateId || x.ParentId == templateId));
            await uow.SaveChangesAsync();
        }

        // Android scales fromAmount and toAmount (and a part's fromAmount); the original amount goes with them, or the rate shown in the dialog would change.
        private static void Multiply(Transaction transaction, int multiplier)
        {
            if (multiplier <= 1)
            {
                return;
            }

            transaction.FromAmount *= multiplier;
            transaction.ToAmount *= multiplier;
            if (transaction.OriginalFromAmount is long original)
            {
                transaction.OriginalFromAmount = original * multiplier;
            }
        }

        // A row loaded with its related entities must not bring them along into an insert.
        private static void DetachNavigations(Transaction transaction)
        {
            transaction.FromAccount = null;
            transaction.ToAccount = null;
            transaction.Category = null;
            transaction.Payee = null;
            transaction.Project = null;
            transaction.Location = null;
            transaction.OriginalCurrency = null;
        }
    }
}
