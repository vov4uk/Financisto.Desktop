using System;
using System.Diagnostics.CodeAnalysis;
using Financisto.Common.Utils;

namespace Financisto.Common.Model
{
    [ExcludeFromCodeCoverage]
    public class BlotterModel : BaseModel
    {
        public string AccountTitle
        {
            get
            {
                if (ToAccountId > 0)
                {
                    // An incoming row has the viewed account on its "from" side; the title reads in the direction the money went.
                    return IsIncomingTransfer
                        ? $"{ToAccountTitle}{BlotterUtils.TRANSFER_DELIMITER}{FromAccountTitle}"
                        : $"{FromAccountTitle}{BlotterUtils.TRANSFER_DELIMITER}{ToAccountTitle}";
                }
                return FromAccountTitle;
            }
        }

        public string AmountTitle
        {
            get
            {
                if (ToAccountId > 0)
                {
                    // Seen from an account (Android's account blotter) a transfer is just the amount that moved in or out of it.
                    return IsAccountPerspective
                        ? BlotterUtils.SetAmountText(FromAccountCurrency, FromAmount, true)
                        : BlotterUtils.GetTransferAmountText(FromAccountCurrency, FromAmount, ToAccountCurrency, ToAmount);
                }

                if (OriginalCurrencyId > 0)
                {
                    return BlotterUtils.SetAmountText(OriginalCurrency, OriginalFromAmount, FromAccountCurrency, FromAmount, true);
                }
                return BlotterUtils.SetAmountText(FromAccountCurrency, FromAmount, true);
            }
        }

        public string AmountClipboardText
        {
            get
            {
                return BlotterUtils.SetAmountText(FromAccountCurrency, FromAmount, true);
            }
        }

        public string BalanceTitle
        {
            get
            {
                if (ToAccountId > 0 && !IsAccountPerspective)
                {
                    return BlotterUtils.SetTransferBalanceText(FromAccountCurrency, FromAccountBalance, ToAccountCurrency, ToAccountBalance);
                }
                return BlotterUtils.SetAmountText(FromAccountCurrency, FromAccountBalance ?? 0, false);
            }
        }

        public bool HasNoCategory => Type != "Transfer" && CategoryId == 0;

        public int? CategoryId { get; set; }
        public string CategoryTitle { get; set; }
        public long Datetime { get; set; }
        public long? FromAccountBalance { get; set; }
        public CurrencyModel FromAccountCurrency { get; set; }
        public int FromAccountCurrencyId { get; set; }
        public int FromAccountId { get; set; }
        public string FromAccountTitle { get; set; }
        public long FromAmount { get; set; }
        public int Id { get; set; }

        /// <summary>The split parent when this row is a part of a split (shown for an account the parent isn't on), else 0.</summary>
        public int ParentId { get; set; }

        /// <summary>The <c>is_transfer</c> column: -1 for the "to" side of a transfer, as the account view returns it (the accounts are swapped).</summary>
        public long IsTransfer { get; set; }

        /// <summary>
        /// The row comes from the account view (<c>v_blotter_for_account_with_splits</c>): it is seen from <see cref="FromAccountId"/>,
        /// whose running balance <see cref="FromAccountBalance"/> is.
        /// </summary>
        public bool IsAccountPerspective { get; set; }

        /// <summary>A transfer row seen from the account it went into.</summary>
        public bool IsIncomingTransfer => IsTransfer == -1;

        public bool IsSplitPart => ParentId > 0;

        public string Location { get; set; }
        public int? LocationId { get; set; }
        public ProjectModel Project { get; set; }
        public string Note { get; set; }
        public CurrencyModel OriginalCurrency { get; set; }
        public int? OriginalCurrencyId { get; set; }
        public long OriginalFromAmount { get; set; }
        public string Payee { get; set; }
        public string Tags { get; set; }
        public long? ToAccountBalance { get; set; }
        public CurrencyModel ToAccountCurrency { get; set; }
        public int? ToAccountCurrencyId { get; set; }
        public int? ToAccountId { get; set; }
        public string ToAccountTitle { get; set; }
        public long ToAmount { get; set; }
        public string TransactionTitle => TransactionTitleUtils.GenerateTransactionTitle(Payee, Note, LocationId > 0 ? Location : string.Empty, CategoryId, CategoryTitle, ToAccountId);

        public bool ShowProjectSeparator => Project != null;

        public bool ShowTagsSeparator => !string.IsNullOrWhiteSpace(Tags);

        public string TagsTitle => string.Join(" | ", (Tags ?? string.Empty).Split("\\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        public string Type
        {
            get
            {
                if (ToAccountId > 0 && CategoryId == 0 && FromAccountId > 0)
                {
                    return "Transfer";
                }
                if (CategoryId == -1)
                {
                    return "Share";
                }
                if (FromAmount > 0)
                {
                    return "Income";
                }
                return "Expense";
            }
        }
    }
}
