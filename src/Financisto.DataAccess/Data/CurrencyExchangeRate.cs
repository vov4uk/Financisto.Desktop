using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Financisto.DataAccess.Data;

namespace Financisto.DataAccess.Data
{
    [DebuggerDisplay("{FromCurrencyId}-{ToCurrencyId} -- {Rate}")]
    [Table(Backup.EXCHANGE_RATES_TABLE)]
    public class CurrencyExchangeRate : Entity, IIdentity
    {
        [Key, Column("from_currency_id"), ForeignKey("FromCurrency")]
        public int FromCurrencyId { get; set; }

        [Key, Column("to_currency_id"), ForeignKey("ToCurrency")]
        public int ToCurrencyId { get; set; }

        [Key, Column("rate_date")]
        public long Date { get; set; }

        [Column("rate")]
        public double Rate { get; set; }

        [Column(Backup.UpdatedOnColumn)]
        public long UpdatedOn { get; set; }

        [Column("remote_key")]
        public string RemoteKey { get; set; }

        public Currency FromCurrency { get; set; }

        public Currency ToCurrency { get; set; }

        [NotMapped]
        public int Id { get; set; } = 1; // Need for backup, on backup export only items with id > 0
    }
}
