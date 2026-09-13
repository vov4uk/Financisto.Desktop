using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Financisto.Desktop.ViewModels;

public partial class AccountsPageViewModel : ViewModelBase
{
    private readonly IFinancistoDatabase _db;

    [ObservableProperty]
    private ObservableCollection<AccountModel> _entities = new();

    public AccountsPageViewModel(IFinancistoDatabase db)
    {
        _db = db;

        _ = RefreshDataAsync();
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (_db == null)
        {
            return;
        }

        using var uow = _db.CreateUnitOfWork();
        var repo = uow.GetRepository<Account>();

        var items = await repo.FindManyAndProjectAsync(
            predicate: x => true,
            projection: acc => new AccountModel(acc),
            includes: x => x.Currency);

        Entities = new ObservableCollection<AccountModel>(
            items.OrderByDescending(x => x.IsActive).ThenBy(x => x.SortOrder));
    }
}
