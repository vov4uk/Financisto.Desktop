using System;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.Common.Utils;
using Financisto.Converters;
using Financisto.DataAccess.Data;

namespace Financisto.Desktop.Data
{
    public class TransferDto : BaseTransactionDto
    {
        private AccountFilterModel fromAccount;
        private int fromAccountId;
        private long fromAmount;
        private bool isAmountNegative = true;
        private AccountFilterModel toAccount;
        private int toAccountId;
        private long toAmount;

        public TransferDto() { }

        /// <summary>
        /// A split part seen from the parent account's side, like Android's SplitTransferActivity.updateUI:
        /// FromAccount is the parent account and ToAccount the other one. A transfer into the parent account
        /// (stored as other account -> parent account) is swapped back and gets IsAmountNegative = false.
        /// </summary>
        public TransferDto(Transaction transaction, int parentAccountId)
            : this(transaction)
        {
            if (transaction.FromAccountId != parentAccountId && transaction.ToAccountId == parentAccountId)
            {
                fromAccountId = transaction.ToAccountId;
                toAccountId = transaction.FromAccountId;
                fromAmount = Math.Abs(transaction.ToAmount);
                toAmount = Math.Abs(transaction.FromAmount);
                fromAccount = DbManual.Account.Find(x => x.Id == fromAccountId);
                toAccount = DbManual.Account.Find(x => x.Id == toAccountId);
                isAmountNegative = false;
            }
        }

        public TransferDto(Transaction transaction)
        {
            id = transaction.Id;
            fromAccountId = transaction.FromAccountId;
            toAccountId = transaction.ToAccountId;
            note = transaction.Note;
            fromAmount = transaction.FromAmount;
            toAmount = transaction.ToAmount;
            date = UnixTimeConverter.Convert(transaction.DateTime).Date;
            time = UnixTimeConverter.Convert(transaction.DateTime);
            fromAccount = DbManual.Account.Find(x => x.Id == fromAccountId);
            toAccount = DbManual.Account.Find(x => x.Id == toAccountId);
        }

        public AccountFilterModel FromAccount
        {
            get => fromAccount;
            set
            {
                if (SetProperty(ref fromAccount, value))
                {
                    RaisePropertyChanged(nameof(FromAccount));
                    RaisePropertyChanged(nameof(RateString));
                    RaisePropertyChanged(nameof(IsToAmountVisible));
                    RaisePropertyChanged(nameof(FromAccountCurrency));
                    RaisePropertyChanged(nameof(SubTransactionTitle));
                }
            }
        }

        public CurrencyModel FromAccountCurrency
        {
            get => DbManual.Currencies?.Find(x => x.Id == (FromAccount != null ? FromAccount.CurrencyId : 0));
        }

        public int FromAccountId
        {
            get => fromAccountId;
            set
            {
                if (SetProperty(ref fromAccountId, value))
                {
                    RaisePropertyChanged(nameof(FromAccountId));
                }
            }
        }

        public long FromAmount
        {
            get => fromAmount;
            set
            {
                if (SetProperty(ref fromAmount, value))
                {
                    RaisePropertyChanged(nameof(FromAmount));
                    RaisePropertyChanged(nameof(RealFromAmount));
                    RecalculateRate();
                }
            }
        }

        /// <summary>
        /// False only for a split part that moves money into the parent account; a standalone transfer is always outgoing.
        /// </summary>
        public override bool IsAmountNegative
        {
            get => isAmountNegative;
            set
            {
                if (SetProperty(ref isAmountNegative, value))
                {
                    RaisePropertyChanged(nameof(IsAmountNegative));
                    RaisePropertyChanged(nameof(RealFromAmount));
                    RaisePropertyChanged(nameof(SubTransactionTitle));
                    RaisePropertyChanged(nameof(FromAccountCaption));
                    RaisePropertyChanged(nameof(ToAccountCaption));
                    RaisePropertyChanged(nameof(FromAmountCaption));
                    RaisePropertyChanged(nameof(ToAmountCaption));
                }
            }
        }

        // For an incoming split part the parent account stays in the first slot, so the captions flip,
        // as Android's RateLayoutView does for a switchable transfer.
        public string FromAccountCaption => LocalizationService.Instance[IsAmountNegative ? "from_account" : "to_account"];
        public string ToAccountCaption => LocalizationService.Instance[IsAmountNegative ? "to_account" : "from_account"];
        public string FromAmountCaption => LocalizationService.Instance[IsAmountNegative ? "amount_out" : "amount_in"];
        public string ToAmountCaption => LocalizationService.Instance[IsAmountNegative ? "amount_in" : "amount_out"];

        public bool IsToAmountVisible => fromAccount != null && toAccount != null && fromAccount.CurrencyId != toAccount.CurrencyId;
        public string RateString
        {
            get
            {
                if (DoubleUtils.DoubleNotEqual(Rate, 0))
                {
                    var d = 1.0 / Rate;
                    return $"1{ToAccountCurrency?.Name}={Rate:F5}{FromAccountCurrency?.Name}, 1{FromAccountCurrency?.Name}={d:F5}{ToAccountCurrency?.Name}";
                }
                return "N/A";
            }
        }

        public override long RealFromAmount => Math.Abs(FromAmount) * (IsAmountNegative ? -1 : 1);
        public override string SubTransactionTitle => IsAmountNegative
            ? $"{FromAccount?.Title}{BlotterUtils.TRANSFER_DELIMITER}{ToAccount?.Title}"
            : $"{ToAccount?.Title}{BlotterUtils.TRANSFER_DELIMITER}{FromAccount?.Title}";
        public AccountFilterModel ToAccount
        {
            get => toAccount;
            set
            {
                if (SetProperty(ref toAccount, value))
                {
                    RaisePropertyChanged(nameof(ToAccount));
                    RaisePropertyChanged(nameof(RateString));
                    RaisePropertyChanged(nameof(IsToAmountVisible));
                    RaisePropertyChanged(nameof(ToAccountCurrency));
                    RaisePropertyChanged(nameof(SubTransactionTitle));
                }
            }
        }

        public CurrencyModel ToAccountCurrency
        {
            get => DbManual.Currencies?.Find(x => x.Id == (ToAccount != null ? ToAccount.CurrencyId : 0));
        }

        public int ToAccountId
        {
            get => toAccountId;
            set { SetProperty(ref toAccountId, value, nameof(ToAccountId)); }
        }
        public long ToAmount
        {
            get => toAmount;
            set
            {
                if (SetProperty(ref toAmount, value))
                {
                    RaisePropertyChanged(nameof(ToAmount));
                    RecalculateRate();
                }
            }
        }

        internal void RecalculateRate()
        {
            if (fromAmount != 0 && toAmount != 0)
            {
                Rate = Math.Abs(fromAmount / 100.0 / (toAmount / 100.0));
            }
        }
    }
}
