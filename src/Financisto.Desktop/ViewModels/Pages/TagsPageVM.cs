using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Helpers;

namespace Financisto.Desktop.ViewModels.Pages
{
    [ExcludeFromCodeCoverage]
    public class TagsPageVM : TagBasePageVM<TagModel>
    {
        public TagsPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        protected override string TitleKey => "tags";

        protected override Task OnAdd() => OpenTagDialogAsync<Project>(0);

        protected override Task OnDelete(TagModel item) => throw new System.NotImplementedException();

        protected override Task OnEdit(TagModel item) => OpenTagDialogAsync<Tag>(item.Id ?? 0);

        protected override async Task RefreshData()
        {
            DbManual.ResetManuals(nameof(DbManual.Tag));
            await DbManual.SetupAsync(db);
            Entities = new ObservableCollection<TagModel>(DbManual.Tag.Where(x => x.Id > 0).OrderByDescending(x => x.IsActive).ThenBy(x => x.Id));
        }
    }
}
