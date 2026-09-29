using Prism.Mvvm;

namespace Financisto.Desktop.Wizards
{
    /// <summary>An editable row of the import and recipes wizards, before it becomes a <c>Transaction</c> / <c>TransactionDto</c>.</summary>
    public class FinancistoTransactionDto : BindableBase
    {
        private int categoryId;
        private int locationId;
        private string note;
        private int order;
        private int projectId;
        private int payeeId;
        private int toAccountId;
        private int fromAccountId;
        private bool isAmountNegative;
        private long? originalFromAmount;

        public int CategoryId
        {
            get => categoryId;
            set
            {
                categoryId = value;
                RaisePropertyChanged(nameof(CategoryId));
            }
        }

        public long DateTime { get; set; }

        public int FromAccountId
        {
            get => fromAccountId;
            set
            {
                fromAccountId = value;
                RaisePropertyChanged(nameof(FromAccountId));
            }
        }

        public long FromAmount { get; set; }

        public int MCC { get; set; }

        public int LocationId
        {
            get => locationId;
            set
            {
                locationId = value;
                RaisePropertyChanged(nameof(LocationId));
            }
        }

        public int? MonoAccountId { get; set; }

        public string Note
        {
            get => note;
            set
            {
                note = value;
                RaisePropertyChanged(nameof(Note));
            }
        }

        public int Order
        {
            get => order;
            set
            {
                order = value;
                RaisePropertyChanged(nameof(Order));
            }
        }
        public int OriginalCurrencyId { get; set; }

        // Notifies so the grid shows the amount the transfer dialog writes back.
        public long? OriginalFromAmount
        {
            get => originalFromAmount;
            set => SetProperty(ref originalFromAmount, value);
        }

        public int ProjectId
        {
            get => projectId;
            set
            {
                projectId = value;
                RaisePropertyChanged(nameof(ProjectId));
            }
        }
        public int PayeeId
        {
            get => payeeId;
            set
            {
                payeeId = value;
                RaisePropertyChanged(nameof(PayeeId));
            }
        }

        public int ToAccountId
        {
            get => toAccountId;
            set
            {
                toAccountId = value;
                RaisePropertyChanged(nameof(ToAccountId));
            }
        }

        public long ToAmount { get; set; }

        public bool IsAmountNegative
        {
            get => isAmountNegative;
            set => SetProperty(ref isAmountNegative, value);
        }
    }
}
