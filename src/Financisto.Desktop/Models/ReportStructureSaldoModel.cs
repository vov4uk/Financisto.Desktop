using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financisto.Desktop.Models;

public class ReportStructureSaldoModel
{
    public DateOnly Date { get; set; }

    public double AssetsDefaultCurrencyBalance { get; set; }

    public double LiabilitiesDefaultCurrencyBalance { get; set; }

    public double NetWorthDefaultCurrencyBalance { get; set; }

    public string DefaultCurrencySymbol { get; set; }
}

public class AccountBalanceRawModel
{
    [Column("account_title")]
    public string Title { get; set; }

    [Column("account_id")]
    public long AccountId { get; set; }

    [Column("is_include_into_totals")]
    public bool AccountIsIncludeInTotals { get; set; }

    [Column("account_type")]
    public string AccountType { get; set; }

    [Column("balance_default_crr")]
    public double? DefaultCurrencyBalance { get; set; }

    [Column("default_crr_symbol")]
    public string DefaultCurrencySymbol { get; set; }
}
