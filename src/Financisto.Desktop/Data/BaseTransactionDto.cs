using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Financisto.Desktop.Data;

public abstract partial class BaseTransactionDto : ObservableObject
{
    [ObservableProperty]
    private DateTimeOffset? _date;

    [ObservableProperty]
    private TimeSpan? _time;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _note;

    private double _rate;

    public DateTime DateTime
    {
        get
        {
            var d = (Date ?? DateTimeOffset.Now).Date;
            return new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Local).Add(Time ?? TimeSpan.Zero);
        }
    }

    public bool IsSubTransaction { get; set; }

    public virtual long RealFromAmount { get; }

    public virtual string SubTransactionTitle { get; }

    public virtual bool IsAmountNegative { get; set; }

    public double Rate
    {
        get => _rate;
        set
        {
            if (SetProperty(ref _rate, value))
            {
                OnPropertyChanged("RateString");
            }
        }
    }
}
