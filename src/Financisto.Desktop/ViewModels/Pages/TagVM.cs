using System.Threading.Tasks;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.ViewModels.Dialogs;

namespace Financisto.Desktop.ViewModels.Pages
{
    public abstract class TagBaseVM<TEntity> : EntityBaseVM<TEntity>
        where TEntity : BaseModel, new()
    {
        protected TagBaseVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        protected async Task OpenTagDialogAsync<T>(int e)
            where T : Tag, new()
        {
            T selectedEntity = await db.GetOrCreateAsync<T>(e);
            TagControlVM context = new TagControlVM(new TagDto(selectedEntity));

            var result = await dialogWrapper.ShowDialogAsync<TagControl>(context, 180, 300, LocalizationService.Instance[typeof(T).Name.ToLowerInvariant()]);

            var updatedItem = result as TagDto;
            if (updatedItem != null)
            {
                selectedEntity.IsActive = updatedItem.IsActive;
                selectedEntity.Title = updatedItem.Title;

                await db.InsertOrUpdateAsync(new[] { selectedEntity });
                await RefreshData();
            }
        }
    }
}
