using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Financisto.Common;
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
    /// <summary>
    /// Non-generic view model contract shared by all "tag-like" pages (payees, projects, ...),
    /// allowing a single view (<see cref="Financisto.Desktop.Views.TagPageView"/>) to be reused
    /// regardless of the concrete entity type.
    /// </summary>
    public interface ITagBaseVM
    {
        string PageTitle { get; }

        IEnumerable<TagBaseModel> Entities { get; }

        bool HasAliases { get; }

        object SelectedValue { get; set; }

        IAsyncCommand AddCommand { get; }

        IAsyncCommand EditCommand { get; }

        IAsyncCommand DeleteCommand { get; }
    }

    public abstract class TagBasePageVM<TEntity> : EntityBaseVM<TEntity>, ITagBaseVM
        where TEntity : TagBaseModel, new()
    {
        protected TagBasePageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
            LocalizationService.Instance.PropertyChanged += OnLocalizationCultureChanged!;
        }

        /// <summary>Localization resource key used for the page header.</summary>
        protected abstract string TitleKey { get; }

        public string PageTitle => LocalizationService.Instance[TitleKey];

        public virtual bool HasAliases => false;

        IEnumerable<TagBaseModel> ITagBaseVM.Entities => Entities;

        object ITagBaseVM.SelectedValue
        {
            get => SelectedValue;
            set => SelectedValue = (TEntity)value;
        }

        private void OnLocalizationCultureChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Item")
            {
                RaisePropertyChanged(nameof(PageTitle));
            }
        }

        protected async Task OpenTagDialogAsync<T>(int e)
            where T : TagBase, new()
        {
            T selectedEntity = await db.GetOrCreateAsync<T>(e);
            TagDialogVM context = new TagDialogVM(new TagDto(selectedEntity));
            double height = context.ShowAliases ? 340 : 180;

            var result = await dialogWrapper.ShowDialogAsync<TagDialog>(context, height, 300, LocalizationService.Instance[typeof(T).Name.ToLowerInvariant()]);

            var updatedItem = result as TagDto;
            if (updatedItem != null)
            {
                selectedEntity.IsActive = updatedItem.IsActive;
                selectedEntity.Title = updatedItem.Title;
                updatedItem.ApplyAliases(selectedEntity);

                await db.InsertOrUpdateAsync(new[] { selectedEntity });
                await RefreshData();
            }
        }
    }
}
