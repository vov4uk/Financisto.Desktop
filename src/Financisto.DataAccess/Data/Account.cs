using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Financisto.DataAccess.Utils;

namespace Financisto.DataAccess.Data
{
    [DebuggerDisplay("{Title}")]
    [Table(Backup.ACCOUNT_TABLE)]
    public class Account : Entity, IIdentity
    {
        [Column(Backup.IdColumn)]
        public int Id { get; set; } = -1;

        [Column(Backup.IsActiveColumn)]
        public bool IsActive { get; set; } = true;

        [Column(Backup.TitleColumn)]
        public string Title { get; set; }

        [Column("creation_date")]
        public long CreationDate { get; set; }

        [Column("last_transaction_date")]
        public long LastTransactionDate { get; set; }

        [Column("last_transaction_id"), Ignore]
        public int LastTransactionId { get; set; }

        [ForeignKey("Currency")]
        [Column("currency_id")]
        public int CurrencyId { get; set; }

        [Column("type")]
        public string Type { get; set; } = "CASH";

        [Column("card_issuer")]
        public string CardIssuer { get; set; }

        [Column("issuer")]
        public string Issuer { get; set; }

        [Column("number")]
        public string Number { get; set; }

        [Column("total_amount")]
        public long TotalAmount { get; set; }

        [Column("total_limit")]
        public long LimitAmount { get; set; }

        [Column(Backup.SortOrderColumn)]
        public int SortOrder { get; set; }

        [Column("is_include_into_totals")]
        public bool IsIncludeIntoTotals { get; set; } = true;

        [Column("last_account_id")]
        public long LastAccountId { get; set; }

        [Column("last_category_id")]
        public long LastCategoryId { get; set; }

        [Column("closing_day")]
        public int ClosingDay { get; set; }

        [Column("payment_day")]
        public int PaymentDay { get; set; }

        [Column("note")]
        public string Note { get; set; }

        [Column(Backup.UpdatedOnColumn)]
        public long UpdatedOn { get; set; }

        // NOT NULL columns: the defaults match Android's, for backups made before they existed.
        [Column("icon")]
        public string Icon { get; set; } = string.Empty;

        [Column("accent_color")]
        public string AccentColor { get; set; } = string.Empty;

        [Column("is_include_into_reports")]
        public bool IsIncludeIntoReports { get; set; } = true;

        public virtual Currency Currency { get; set; }
    }
}
