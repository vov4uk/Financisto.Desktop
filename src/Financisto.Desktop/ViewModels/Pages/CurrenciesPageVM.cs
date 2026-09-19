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
using NLog;

namespace Financisto.Desktop.ViewModels.Pages
{
    [ExcludeFromCodeCoverage]
    public class CurrenciesPageVM : EntityBaseVM<CurrencyModel>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public CurrenciesPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        protected override async Task OnAdd()
        {
            var addVm = new NewCurrencyDialogVM();
            var selection = await dialogWrapper.ShowDialogAsync<NewCurrencyDialog>(addVm, 400, 340, LocalizationService.Instance.currencies)
                            as CurrencyTemplateItem;

            if (selection == null)
                return;

            if (selection.IsNewCurrency)
            {
                await OpenCurrencyDialogAsync(0);
            }
            else
            {
                await SaveFromTemplateAsync(selection.Template);
            }
        }

        protected override async Task OnDelete(CurrencyModel item)
        {
            int id = item.Id ?? 0;
            if (id == 0) return;

            using (var uow = db.CreateUnitOfWork())
            {
                var accountRepo = uow.GetRepository<Account>();
                var usedByAccount = await accountRepo.FindByAsync(a => a.CurrencyId == id);
                if (usedByAccount != null)
                {
                    await dialogWrapper.ShowMessageBoxAsync(LocalizationService.Instance.currency_is_used, LocalizationService.Instance.delete);
                    return;
                }

                var txRepo = uow.GetRepository<Transaction>();
                var usedByTx = await txRepo.FindByAsync(t => t.OriginalCurrencyId == id);
                if (usedByTx != null)
                {
                    await dialogWrapper.ShowMessageBoxAsync(LocalizationService.Instance.currency_is_used, LocalizationService.Instance.delete);
                    return;
                }

                if (!await dialogWrapper.ShowMessageBoxAsync(LocalizationService.Instance.confirm_delete_currency, LocalizationService.Instance.delete, true))
                    return;

                var currencyRepo = uow.GetRepository<Currency>();
                var entity = await currencyRepo.FindByAsync(c => c.Id == id);
                if (entity != null)
                {
                    Logger.Info($"Deleting currency id={id}");
                    await currencyRepo.DeleteAsync(entity);
                    await uow.SaveChangesAsync();
                }
            }

            await RefreshData();
        }

        protected override Task OnEdit(CurrencyModel item) => OpenCurrencyDialogAsync(item.Id ?? 0);

        protected override async Task RefreshData()
        {
            DbManual.ResetManuals(nameof(DbManual.Currencies));
            await DbManual.SetupAsync(db);
            Entities = new ObservableCollection<CurrencyModel>(DbManual.Currencies.Where(x => x.Id.HasValue));
        }

        private async Task OpenCurrencyDialogAsync(int id)
        {
            Currency entity = await db.GetOrCreateAsync<Currency>(id);
            var dto = new CurrencyDto(entity);
            if (id == 0)
                dto.UpdateExchangeRate = true;

            var vm = new CurrencyDialogVM(dto);
            var result = await dialogWrapper.ShowDialogAsync<NewCurrencyDialog>(vm, 340, 440, LocalizationService.Instance.currency);

            var updated = result as CurrencyDto;
            if (updated == null)
                return;

            entity.Title = updated.Title;
            entity.Name = updated.Name;
            entity.Symbol = updated.Symbol;
            entity.IsDefault = updated.IsDefault;
            entity.UpdateExchangeRate = updated.UpdateExchangeRate;
            entity.Decimals = updated.Decimals;
            entity.DecimalSeparator = updated.DecimalSeparator;
            entity.GroupSeparator = updated.GroupSeparator;
            entity.SymbolFormat = updated.SymbolFormat.ToString();
            entity.NumberFormat = updated.NumberFormat;

            await db.InsertOrUpdateAsync(new[] { entity });
            await RefreshData();
        }

        private async Task SaveFromTemplateAsync(System.Collections.Generic.List<string> template)
        {
            Currency entity = await db.GetOrCreateAsync<Currency>(0);
            entity.Name = template[0];
            entity.Title = template[1];
            entity.Symbol = template[2];
            entity.Decimals = int.TryParse(template[3], out int d) ? System.Math.Clamp(d, 0, 3) : 2;
            entity.DecimalSeparator = template[4];
            entity.GroupSeparator = template[5];
            entity.IsActive = true;
            entity.IsDefault = !DbManual.Currencies.Any(x => x.Id.HasValue);

            await db.InsertOrUpdateAsync(new[] { entity });
            await RefreshData();
        }
    }
}
