using Financisto.DataAccess.Data;
using Prism.Mvvm;

namespace Financisto.Desktop.Data
{
    /// <summary>Editable copy of an Android-compatible SMS template. Ids use null for "not set" (0 / -1 in the table).</summary>
    public class SmsTemplateDto : BindableBase
    {
        private string description = string.Empty;
        private string note = string.Empty;
        private string sender = string.Empty;
        private string template = string.Empty;
        private bool isActive = true;
        private bool isIncome;
        private bool matchGroupSummary;
        private int? accountId;
        private int? categoryId;
        private int? locationId;
        private int? payeeId;
        private int? projectId;
        private int? toAccountId;

        public SmsTemplateDto()
        {
        }

        public SmsTemplateDto(SmsTemplate entity)
        {
            Id = entity.Id;
            IsActive = entity.IsActive;
            Sender = entity.Title ?? string.Empty;
            Description = entity.Description ?? string.Empty;
            Template = entity.Template ?? string.Empty;
            Note = entity.Note ?? string.Empty;
            MatchGroupSummary = entity.MatchGroupSummary;
            IsIncome = entity.IsIncome;
            AccountId = entity.AccountId > 0 ? entity.AccountId : null;
            ToAccountId = entity.ToAccountId > 0 ? entity.ToAccountId : null;
            CategoryId = entity.CategoryId > 0 ? entity.CategoryId : null;
            PayeeId = entity.PayeeId > 0 ? entity.PayeeId : null;
            ProjectId = entity.ProjectId > 0 ? entity.ProjectId : null;
            LocationId = entity.LocationId > 0 ? entity.LocationId : null;
        }

        /// <summary>0 for a template that has not been saved yet.</summary>
        public int Id { get; set; }

        public bool IsActive
        {
            get => isActive;
            set => SetProperty(ref isActive, value);
        }

        /// <summary>The notification sender package / title / SMS number; the table's title column.</summary>
        public string Sender
        {
            get => sender;
            set => SetProperty(ref sender, value);
        }

        public string Description
        {
            get => description;
            set => SetProperty(ref description, value);
        }

        public string Template
        {
            get => template;
            set => SetProperty(ref template, value);
        }

        public string Note
        {
            get => note;
            set => SetProperty(ref note, value);
        }

        public bool MatchGroupSummary
        {
            get => matchGroupSummary;
            set => SetProperty(ref matchGroupSummary, value);
        }

        public bool IsIncome
        {
            get => isIncome;
            set => SetProperty(ref isIncome, value);
        }

        public int? AccountId
        {
            get => accountId;
            set => SetProperty(ref accountId, value);
        }

        public int? ToAccountId
        {
            get => toAccountId;
            set => SetProperty(ref toAccountId, value);
        }

        public int? CategoryId
        {
            get => categoryId;
            set => SetProperty(ref categoryId, value);
        }

        public int? PayeeId
        {
            get => payeeId;
            set => SetProperty(ref payeeId, value);
        }

        public int? ProjectId
        {
            get => projectId;
            set => SetProperty(ref projectId, value);
        }

        public int? LocationId
        {
            get => locationId;
            set => SetProperty(ref locationId, value);
        }

        /// <summary>Copies the edited values onto <paramref name="entity"/>, leaving the columns the dialog does not show (remote key, sort order).</summary>
        public void ApplyTo(SmsTemplate entity)
        {
            entity.IsActive = IsActive;
            entity.Title = Sender?.Trim() ?? string.Empty;
            entity.Description = Description ?? string.Empty;
            entity.Template = Template ?? string.Empty;
            entity.Note = Note ?? string.Empty;
            entity.MatchGroupSummary = MatchGroupSummary;
            entity.IsIncome = IsIncome;
            entity.AccountId = AccountId ?? -1;
            entity.ToAccountId = ToAccountId ?? -1;
            entity.CategoryId = CategoryId ?? 0;
            entity.PayeeId = PayeeId ?? 0;
            entity.ProjectId = ProjectId ?? 0;
            entity.LocationId = LocationId ?? 0;
        }
    }
}
