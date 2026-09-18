using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.ViewModels.Dialogs;

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

        IEnumerable<TagModel> Entities { get; }

        object SelectedValue { get; set; }

        IAsyncCommand AddCommand { get; }

        IAsyncCommand EditCommand { get; }

        IAsyncCommand DeleteCommand { get; }
    }

    public abstract class TagBaseVM<TEntity> : EntityBaseVM<TEntity>, ITagBaseVM
        where TEntity : TagModel, new()
    {
        protected TagBaseVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
            LocalizationService.Instance.PropertyChanged += OnLocalizationCultureChanged;
        }

        /// <summary>Localization resource key used for the page header.</summary>
        protected abstract string TitleKey { get; }

        public string PageTitle => LocalizationService.Instance[TitleKey];

        IEnumerable<TagModel> ITagBaseVM.Entities => Entities;

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
