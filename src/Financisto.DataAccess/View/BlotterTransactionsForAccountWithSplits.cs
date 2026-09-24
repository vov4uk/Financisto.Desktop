using System.ComponentModel.DataAnnotations.Schema;

namespace Financisto.DataAccess.View
{
    public class BlotterTransactionsForAccountWithSplits : TransactionsView
    {
        [Column("parent_account_id")]
        public int ParentAccountId { get; set; }
    }
}
