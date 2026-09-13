using CommunityToolkit.Mvvm.ComponentModel;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.Common.Utils;
using Financisto.Converters;
using Financisto.DataAccess.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Financisto.Desktop.Data;

public partial class TransactionDto : BaseTransactionDto
{
    private CategoryModel _category;
    private CurrencyModel _originalCurrency;
    private AccountFilterModel _fromAccount;
    private bool _isAmountNegative;
    private long _unSplitAmount;
    private ObservableCollection<BaseTransactionDto> _subTransactions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSplitCategory))]
    private int? _categoryId;

    [ObservableProperty]
    private int _fromAccountId;

    [ObservableProperty]
    private long _fromAmount;

    [ObservableProperty]
    private int? _locationId;

    [ObservableProperty]
    private int? _originalCurrencyId;

    [ObservableProperty]
    private long? _originalFromAmount;

    [ObservableProperty]
    private long _parentTransactionUnSplitAmount;

    [ObservableProperty]
    private int? _payeeId;

    [ObservableProperty]
    private int? _projectId;

    public TransactionDto() { }

    public TransactionDto(Transaction transaction, IEnumerable<Transaction> subTransactions)
        : this(transaction)
    {
        var list = new List<BaseTransactionDto>();
        foreach (var t in subTransactions)
        {
            if (t.ToAccountId > 0 && t.CategoryId == 0 && t.FromAccountId > 0)
            {
                list.Add(new TransferDto(t));
            }
            else
            {
                var tr = new TransactionDto(t);

                // if transaction not in home currency, replace FromAmount with OriginalFromAmount to show correct values
                if (IsOriginalFromAmountVisible)
                {
                    tr.FromAmount = tr.OriginalFromAmount ?? 0;
                }
                list.Add(tr);
            }
        }

        SubTransactions = new ObservableCollection<BaseTransactionDto>(list);
    }

    public TransactionDto(Transaction transaction)
    {
        Id = transaction.Id;
        _fromAccountId = transaction.FromAccountId;
        _categoryId = transaction.CategoryId;
        _payeeId = transaction.PayeeId;
        _originalCurrencyId = transaction.OriginalCurrencyId;
        _originalFromAmount = transaction.OriginalFromAmount;
        _locationId = transaction.LocationId;
        _projectId = transaction.ProjectId;
        Note = transaction.Note;
        _fromAmount = transaction.FromAmount;
        _isAmountNegative = transaction.FromAmount <= 0;
        var dt = UnixTimeConverter.Convert(transaction.DateTime);
        Date = new DateTimeOffset(dt.Date);
        Time = dt.TimeOfDay;
    }

    public CategoryModel Category
    {
        get => _category ??= DbManual.Category?.Find(x => x.Id == CategoryId);
        set
        {
            if (SetProperty(ref _category, value))
            {
                if (_category is { Id: > 0 })
                {
                    IsAmountNegative = _category.Type == 0;
                }
            }
        }
    }

    public AccountFilterModel FromAccount
    {
        get => _fromAccount ??= DbManual.Account?.Find(x => x.Id == FromAccountId);
        set
        {
            if (SetProperty(ref _fromAccount, value))
            {
                OnPropertyChanged(nameof(IsOriginalFromAmountVisible));
                OnPropertyChanged(nameof(RateString));
                OnPropertyChanged(nameof(FromAccountCurrency));
            }
        }
    }

    public CurrencyModel FromAccountCurrency
        => DbManual.Currencies?.Find(x => x.Id == (FromAccount != null ? FromAccount.CurrencyId : 0));

    public override bool IsAmountNegative
    {
        get => _isAmountNegative;
        set
        {
            if (SetProperty(ref _isAmountNegative, value))
            {
                RecalculateUnSplitAmount();
            }
        }
    }

    public bool IsOriginalFromAmountVisible => OriginalCurrency != null && OriginalCurrency.Id != null && FromAccount != null && OriginalCurrency.Id != FromAccount.CurrencyId;

    public bool IsSplitCategory => CategoryId == -1;

    public CurrencyModel OriginalCurrency
    {
        get => _originalCurrency ??= DbManual.Currencies?.Find(x => x.Id == OriginalCurrencyId);
        set
        {
            if (SetProperty(ref _originalCurrency, value))
            {
                OnPropertyChanged(nameof(IsOriginalFromAmountVisible));
                OnPropertyChanged(nameof(RateString));
            }
        }
    }

    public string RateString
    {
        get
        {
            if (DoubleUtils.DoubleNotEqual(Rate, 0))
            {
                var d = 1.0 / Rate;
                var localCurrency = DbManual.Currencies?.Find(x => x.Id == FromAccount?.CurrencyId);
                return $"1{OriginalCurrency?.Name}={Rate:F5}{localCurrency?.Name}, 1{localCurrency?.Name}={d:F5}{OriginalCurrency?.Name}";
            }

            return "N/A";
        }
    }

    public override long RealFromAmount => Math.Abs(IsOriginalFromAmountVisible ? (OriginalFromAmount ?? 0) : FromAmount) * (IsAmountNegative ? -1 : 1);

    public long SplitAmount => _subTransactions?.Sum(x => x.RealFromAmount) ?? 0;

    public ObservableCollection<BaseTransactionDto> SubTransactions
    {
        get => _subTransactions;
        private set
        {
            if (SetProperty(ref _subTransactions, value))
            {
                RecalculateUnSplitAmount();
            }
        }
    }

    public override string SubTransactionTitle => Category?.Title ?? string.Empty;

    public long UnsplitAmount
    {
        get => _unSplitAmount;
        private set => SetProperty(ref _unSplitAmount, value);
    }

    public void RecalculateUnSplitAmount()
    {
        UnsplitAmount = !IsSubTransaction ? RealFromAmount - SplitAmount : ParentTransactionUnSplitAmount - RealFromAmount;
    }

    internal void RecalculateRate()
    {
        if (OriginalFromAmount != null && OriginalFromAmount != 0)
        {
            Rate = Math.Abs(FromAmount / 100.0 / (OriginalFromAmount.Value / 100.0));
        }
    }

    partial void OnFromAmountChanged(long value)
    {
        RecalculateRate();
        RecalculateUnSplitAmount();
    }

    partial void OnOriginalFromAmountChanged(long? value) => RecalculateRate();

    partial void OnParentTransactionUnSplitAmountChanged(long value) => RecalculateUnSplitAmount();
}
