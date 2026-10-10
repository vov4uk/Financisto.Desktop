using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Utils;
using Financisto.Desktop.Data;
using Prism.Commands;

namespace Financisto.Desktop.ViewModels.Dialogs
{
    public class TransferDialogVM : DialogBaseVM
    {
        private readonly string[] TrackingProperies = new string[]
        {
            nameof(TransferDto.FromAmount),
            nameof(TransferDto.ToAccount),
            nameof(TransferDto.FromAccount),
            nameof(TransferDto.TemplateName),
        };
        private readonly IReadOnlyDictionary<int, long> accountBalances;
        private DelegateCommand _changeFromAmountSignCommand;
        private DelegateCommand _clearNotesCommand;

        /// <param name="accountBalances">Account id → current balance (<c>IFinancistoDatabase.GetLastRunningBalancesAsync</c>), read right before
        /// the dialog opens; shown under the From account combobox. Null (e.g. a split part) shows no balance.</param>
        public TransferDialogVM(TransferDto transfer, IReadOnlyDictionary<int, long> accountBalances = null)
        {
            Transfer = transfer;
            this.accountBalances = accountBalances;
            Transfer.PropertyChanged += TransferPropertyChanged;
            transfer.RecalculateRate();
        }

        public TransferDto Transfer { get; }

        /// <summary>The From account's current balance, formatted like the accounts grid (AccountModel.AmountTitle).</summary>
        public string FromAccountBalance =>
            FromAccountBalanceValue is long balance ? BlotterUtils.SetAmountText(Transfer.FromAccountCurrency, balance, false) : null;

        public bool IsFromAccountBalanceNegative => FromAccountBalanceValue < 0;

        private long? FromAccountBalanceValue =>
            accountBalances != null && Transfer.FromAccount?.Id is int id ? accountBalances.GetValueOrDefault(id) : null;

        // Only a split part can go either way: out of the parent account (-) or into it (+).
        public DelegateCommand ChangeFromAmountSignCommand => _changeFromAmountSignCommand ??= new DelegateCommand(
            () => { Transfer.IsAmountNegative = !Transfer.IsAmountNegative; },
            () => Transfer.IsSubTransaction);

        public DelegateCommand ClearNotesCommand => _clearNotesCommand ??= new DelegateCommand(() => { Transfer.Note = default; });

        public override object OnRequestSave() => Transfer;

        // A template needs a name to be found by in the list.
        protected override bool CanSaveCommandExecute()
            => Transfer.FromAccount != null && Transfer.ToAccount != null && Transfer.FromAccountId != Transfer.ToAccountId
               && (!Transfer.IsTemplate || !string.IsNullOrWhiteSpace(Transfer.TemplateName));

        private void TransferPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (TrackingProperies.Contains(e.PropertyName))
            {
                SaveCommand.NotifyCanExecuteChanged();
            }
            if (e.PropertyName == nameof(TransferDto.FromAccount))
            {
                OnPropertyChanged(nameof(FromAccountBalance));
                OnPropertyChanged(nameof(IsFromAccountBalanceNegative));
            }
        }
    }
}
