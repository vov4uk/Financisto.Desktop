using System;
using Financisto.Common.Model;

namespace Financisto.Desktop.Models;

// port of tw.tib.financisto.model.Total (balance part only)
public class Total
{
    public Total(CurrencyModel currency, TotalError error = null)
    {
        Currency = currency;
        Error = error;
    }

    public CurrencyModel Currency { get; }

    public TotalError Error { get; }

    public long Balance { get; set; }

    public bool IsError => Error != null;
}

// port of tw.tib.financisto.model.TotalError
public class TotalError
{
    private TotalError(CurrencyModel currency, long dateTime)
    {
        Currency = currency;
        DateTime = dateTime;
    }

    public static TotalError LastRateError(CurrencyModel currency) =>
        new(currency, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    public CurrencyModel Currency { get; }

    public long DateTime { get; }
}

// one row of the dashboard totals list (the Android "totals details" screen)
public record AccountsTotalItemModel
{
    public string Title { get; init; }

    public string Amount { get; init; }

    public bool IsNegative { get; init; }

    public bool IsError { get; init; }

    // exchange rate info or error message
    public string Details { get; init; }

    // amount converted to the home currency
    public string ConvertedAmount { get; init; }
}
