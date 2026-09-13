using CommunityToolkit.Mvvm.ComponentModel;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.Common.Utils;
using Financisto.Converters;
using Financisto.DataAccess.Data;
using System;

namespace Financisto.Desktop.Data;

public partial class TransferDto : BaseTransactionDto
{
    private AccountFilterModel _fromAccount;
    private AccountFilterModel _toAccount;

    [ObservableProperty]
    private int _fromAccountId;

    [ObservableProperty]
    private long _fromAmount;

    [ObservableProperty]
    private int _toAccountId;

    [ObservableProperty]
    private long _toAmount;

    public TransferDto() { }

    public TransferDto(Transaction transaction)
    {
        Id = transaction.Id;
        _fromAccountId = transaction.FromAccountId;
        _toAccountId = transaction.ToAccountId;
        Note = transaction.Note;
        _fromAmount = transaction.FromAmount;
        _toAmount = transaction.ToAmount;
        var dt = UnixTimeConverter.Convert(transaction.DateTime);
        Date = new DateTimeOffset(dt.Date);
        Time = dt.TimeOfDay;
        _fromAccount = DbManual.Account?.Find(x => x.Id == _fromAccountId);
        _toAccount = DbManual.Account?.Find(x => x.Id == _toAccountId);
    }

    public AccountFilterModel FromAccount
    {
        get => _fromAccount ??= DbManual.Account?.Find(x => x.Id == FromAccountId);
        set
        {
            if (SetProperty(ref _fromAccount, value))
            {
                OnPropertyChanged(nameof(RateString));
                OnPropertyChanged(nameof(IsToAmountVisible));
                OnPropertyChanged(nameof(FromAccountCurrency));
            }
        }
    }

    public CurrencyModel FromAccountCurrency => DbManual.Currencies?.Find(x => x.Id == (FromAccount != null ? FromAccount.CurrencyId : 0));

    public override bool IsAmountNegative => true;

    public bool IsToAmountVisible => _fromAccount != null && _toAccount != null && _fromAccount.CurrencyId != _toAccount.CurrencyId;

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

    public override long RealFromAmount => -1 * Math.Abs(FromAmount);

    public override string SubTransactionTitle => $"{FromAccount?.Title}{BlotterUtils.TRANSFER_DELIMITER}{ToAccount?.Title}";

    public AccountFilterModel ToAccount
    {
        get => _toAccount ??= DbManual.Account?.Find(x => x.Id == ToAccountId);
        set
        {
            if (SetProperty(ref _toAccount, value))
            {
                OnPropertyChanged(nameof(RateString));
                OnPropertyChanged(nameof(IsToAmountVisible));
                OnPropertyChanged(nameof(ToAccountCurrency));
            }
        }
    }

    public CurrencyModel ToAccountCurrency => DbManual.Currencies?.Find(x => x.Id == (ToAccount != null ? ToAccount.CurrencyId : 0));

    internal void RecalculateRate()
    {
        if (FromAmount != 0 && ToAmount != 0)
        {
            Rate = Math.Abs(FromAmount / 100.0 / (ToAmount / 100.0));
        }
    }

    partial void OnFromAmountChanged(long value) => RecalculateRate();

    partial void OnToAmountChanged(long value) => RecalculateRate();
}
