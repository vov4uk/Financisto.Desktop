using System.ComponentModel.DataAnnotations.Schema;

namespace Financisto.DataAccess.Data
{
    [Table(Backup.SMS_TEMPLATES_TABLE)]
    public class SmsTemplate : Entity, IIdentity
    {
        [Column(Backup.IdColumn)]
        public int Id { get; set; } = -1;

        [Column(Backup.IsActiveColumn)]
        public bool IsActive { get; set; } = true;

        [Column(Backup.TitleColumn)]
        public string Title { get; set; }

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [ForeignKey("Category")]
        [Column("category_id")]
        public int CategoryId { get; set; }

        [Column("template")]
        public string Template { get; set; }

        [ForeignKey("Account")]
        [Column("account_id")]
        public int AccountId { get; set; }

        [Column("to_account_id")]
        public int ToAccountId { get; set; } = -1;

        [Column("payee_id")]
        public int PayeeId { get; set; }

        [Column("project_id")]
        public int ProjectId { get; set; }

        [Column("note")]
        public string Note { get; set; }

        [Column("is_income")]
        public bool IsIncome { get; set; }

        [Column(Backup.SortOrderColumn)]
        public int SortOrder { get; set; }

        [Column("remote_key")]
        public string RemoteKey { get; set; }

        [Column(Backup.UpdatedOnColumn)]
        public long UpdatedOn { get; set; }

        public Account Account { get; set; }

        public Category Category { get; set; }
    }
}
