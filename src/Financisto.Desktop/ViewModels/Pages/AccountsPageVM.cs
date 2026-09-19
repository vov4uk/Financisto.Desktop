using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.Views.Dialogs;

namespace Financisto.Desktop.ViewModels.Pages
{
    [ExcludeFromCodeCoverage]
    public class AccountsPageVM : EntityBaseVM<AccountModel>
    {
        public AccountsPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        protected override Task OnAdd() => OpenAccountDialogAsync(0);

        protected override Task OnEdit(AccountModel item) => OpenAccountDialogAsync(item.Id ?? 0);

        protected override async Task OnDelete(AccountModel item)
        {
            if (! await dialogWrapper.ShowMessageBoxAsync(
                    LocalizationService.Instance.confirm_delete_account,
                    LocalizationService.Instance.delete,
                    yesNoButtons: true))
                return;

            var account = await db.GetOrCreateAsync<Account>(item.Id ?? 0);
            account.IsActive = false;
            await db.InsertOrUpdateAsync(new[] { account });

            DbManual.ResetManuals(nameof(DbManual.Account));
            await DbManual.SetupAsync(db);
            await RefreshData();
        }

        protected override async Task RefreshData()
        {
            using var uow = db.CreateUnitOfWork();
            var accountRepo = uow.GetRepository<Account>();
            var items = await accountRepo.FindManyAndProjectAsync(
                predicate: x => true,
                projection: acc => new AccountModel(acc),
                includes: x => x.Currency);

            Entities = new ObservableCollection<AccountModel>(
                items.OrderByDescending(x => x.IsActive).ThenBy(x => x.SortOrder));
        }

        private async Task OpenAccountDialogAsync(int id)
        {
            var isNew = id == 0;
            Account account = await db.GetOrCreateAsync<Account>(id);

            AccountDto dto;
            if (isNew)
            {
                dto = new AccountDto
                {
                    Type = "CASH",
                    IsActive = true,
                    IsIncludeIntoTotals = true,
                };
            }
            else
            {
                dto = new AccountDto(account);
            }

            var vm = new AccountDialogVM(dto, isNew);
            var result = await dialogWrapper.ShowDialogAsync<AccountDialog>(
                vm, 580, 560, LocalizationService.Instance["account"]);

            if (result is not AccountDto updated)
                return;

            ApplyDto(account, updated);
            await db.InsertOrUpdateAsync(new[] { account });

            if (isNew && updated.OpeningAmount != 0)
            {
                var t = new Transaction
                {
                    FromAccountId = account.Id,
                    CategoryId = 0,
                    FromAmount = updated.OpeningAmount,
                    DateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                };
                await db.InsertOrUpdateAsync(new[] { t });
            }

            DbManual.ResetManuals(nameof(DbManual.Account));
            await DbManual.SetupAsync(db);
            await RefreshData();
        }

        private static void ApplyDto(Account account, AccountDto dto)
        {
            account.Title = dto.Title;
            account.IsActive = dto.IsActive;
            account.Type = dto.Type;
            account.CurrencyId = dto.CurrencyId;
            account.CardIssuer = dto.CardIssuer;
            account.Issuer = dto.Issuer;
            account.Number = dto.Number;
            account.LimitAmount = dto.LimitAmount;
            account.SortOrder = dto.SortOrder;
            account.IsIncludeIntoTotals = dto.IsIncludeIntoTotals;
            account.Note = dto.Note;
            account.ClosingDay = dto.ClosingDay;
            account.PaymentDay = dto.PaymentDay;
        }
    }
}
