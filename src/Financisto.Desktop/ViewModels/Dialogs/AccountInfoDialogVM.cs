using System;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.Common.Utils;
using Financisto.Converters;
using Financisto.DataAccess.Data;

namespace Financisto.Desktop.ViewModels.Dialogs;

/// <summary>
/// Read-only account details, like Android's <c>AccountInfoDialog</c>. "Edit" (the save command) returns the account id so the caller
/// can open the edit dialog; "Close" cancels.
/// </summary>
public class AccountInfoDialogVM : DialogBaseVM
{
    private readonly int accountId;

    /// <param name="account">The account with its <see cref="Account.Currency"/> loaded.</param>
    public AccountInfoDialogVM(Account account)
    {
        accountId = account.Id;
        Title = account.Title;
        Type = account.Type;
        CardIssuer = account.CardIssuer;
        Note = account.Note;

        var type = System.Enum.TryParse<AccountType>(account.Type, out var parsedType) ? parsedType : AccountType.OTHER;
        TypeTitle = type.GetEnumDescription();
        IsCard = type is AccountType.DEBIT_CARD or AccountType.CREDIT_CARD;
        Issuer = $"{account.Issuer} {(string.IsNullOrEmpty(account.Number) ? string.Empty : "#" + account.Number)}".Trim();

        var currency = account.Currency != null ? new CurrencyModel(account.Currency) : null;
        CurrencyTitle = currency?.Title;

        if (type == AccountType.CREDIT_CARD && account.LimitAmount != 0)
        {
            // A credit card with a limit: the amount owed, and what is left of the limit.
            ShowAmount = true;
            AmountText = BlotterUtils.SetAmountText(currency, account.TotalAmount, true);
            IsAmountNegative = account.TotalAmount < 0;

            var balance = Math.Abs(account.LimitAmount) + account.TotalAmount;
            BalanceText = BlotterUtils.SetAmountText(currency, balance, true);
            IsBalanceNegative = balance < 0;
        }
        else
        {
            BalanceText = BlotterUtils.SetAmountText(currency, account.TotalAmount, true);
            IsBalanceNegative = account.TotalAmount < 0;
        }
    }

    public string Title { get; }

    /// <summary>Account type name and card issuer name: what <c>AccountTypeConverter</c> turns into the type's icon.</summary>
    public string Type { get; }

    public string CardIssuer { get; }

    public string TypeTitle { get; }

    public bool IsCard { get; }

    public string Issuer { get; }

    public string CurrencyTitle { get; }

    public bool ShowAmount { get; }

    public string AmountText { get; }

    public bool IsAmountNegative { get; }

    public string BalanceText { get; }

    public bool IsBalanceNegative { get; }

    public string Note { get; }

    public bool HasNote => !string.IsNullOrEmpty(Note);

    public override object OnRequestSave() => accountId;
}
