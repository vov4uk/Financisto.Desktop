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
    public class ProjectsVM : TagBaseVM<ProjectModel>
    {
        public ProjectsVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        protected override Task OnAdd() => OpenTagDialogAsync<Project>(0);

        protected override Task OnDelete(ProjectModel item) => throw new System.NotImplementedException();

        protected override Task OnEdit(ProjectModel item) => OpenTagDialogAsync<Project>(item.Id ?? 0);

        protected override async Task RefreshData()
        {
            DbManual.ResetManuals(nameof(DbManual.Project));
            await DbManual.SetupAsync(db);
            Entities = new ObservableCollection<ProjectModel>(DbManual.Project.Where(x => x.Id > 0).OrderByDescending(x => x.IsActive).ThenBy(x => x.Id));
        }
    }
}
