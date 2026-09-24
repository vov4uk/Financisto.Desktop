using Financisto.Converters;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Data;
using System;
using System.Linq;

namespace Financisto.Desktop.Helpers;

public static class MapperHelper
{
    public static void MapTransfer(TransferDto dto, Transaction tr)
    {
        var firstAmount = Math.Abs(dto.FromAmount);
        var secondAmount = Math.Abs(dto.IsToAmountVisible ? dto.ToAmount : dto.FromAmount);
        if (dto.IsAmountNegative)
        {
            tr.FromAccountId = dto.FromAccountId;
            tr.ToAccountId = dto.ToAccountId;
            tr.FromAmount = -firstAmount;
            tr.ToAmount = secondAmount;
        }
        else
        {
            // A split part into the parent account is stored as other account -> parent account,
            // swapped like Android's SplitTransferActivity.updateFromUI.
            tr.FromAccountId = dto.ToAccountId;
            tr.ToAccountId = dto.FromAccountId;
            tr.FromAmount = -secondAmount;
            tr.ToAmount = firstAmount;
        }

        tr.FromAccount = null;
        tr.ToAccount = null;
        tr.Note = dto.Note;
        tr.DateTime = UnixTimeConverter.ConvertBack(dto.DateTime);
        tr.LastRecurrence = UnixTimeConverter.ConvertBack(DateTime.Now);

        if (dto.FromAccountCurrency?.Id != dto.ToAccountCurrency?.Id)
        {
            tr.OriginalCurrencyId = dto.IsAmountNegative ? dto.FromAccountCurrency?.Id : dto.ToAccountCurrency?.Id;
            tr.OriginalFromAmount = tr.FromAmount;
        }
        else
        {
            tr.OriginalCurrencyId = 0;
            tr.OriginalFromAmount = 0;
        }

        tr.CategoryId = 0;
        tr.Category = default;
    }

    public static void MapTransaction(TransactionDto dto, Transaction tr)
    {
        tr.FromAccountId = dto.FromAccountId;

        if (dto.OriginalCurrencyId > 0 && dto.FromAccount?.CurrencyId == dto.OriginalCurrencyId)
        {
            tr.FromAmount = dto.RealFromAmount;
            tr.OriginalCurrencyId = 0;
            tr.OriginalFromAmount = 0;
        }
        else
        {
            tr.FromAmount = Math.Abs(dto.FromAmount) * (dto.IsAmountNegative ? -1 : 1);
            tr.OriginalFromAmount = Math.Abs(dto.OriginalFromAmount ?? 0) * (dto.IsAmountNegative ? -1 : 1);
            tr.OriginalCurrencyId = dto.OriginalCurrencyId ?? 0;
        }

        tr.CategoryId = dto.CategoryId ?? 0;
        tr.Category = default;
        tr.Location = default;
        tr.Project = default;
        tr.OriginalCurrency = default;
        tr.FromAccount = default;
        tr.ToAccount = default;
        tr.PayeeId = dto.PayeeId ?? 0;
        tr.LocationId = dto.LocationId ?? 0;
        tr.ProjectId = dto.CategoryId == -1 ? 0 : (dto.ProjectId ?? 0); // parent transaction doesn't have a Project
        tr.Note = dto.Note;
        tr.Tags = dto.SelectedTags?.Count > 0 ? string.Join(TransactionDto.TagsDelimiter, dto.SelectedTags.Select(t => t.Title)) : null;
        tr.DateTime = UnixTimeConverter.ConvertBack(dto.DateTime);
        tr.LastRecurrence = UnixTimeConverter.ConvertBack(DateTime.Now);
    }
}
