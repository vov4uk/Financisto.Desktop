using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.Desktop.Helpers;

namespace Financisto.Desktop.ViewModels;

[ExcludeFromCodeCoverage]
public abstract class EntityBaseVM<T> : BaseViewModel<T>
       where T : BaseModel, new()
{
    private IAsyncCommand _addCommand;
    private IAsyncCommand _deleteCommand;
    private IAsyncCommand _editCommand;
    private T _selectedValue;
    protected readonly IDialogWrapper dialogWrapper;

    protected EntityBaseVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
        : base(db)
    {
        this.dialogWrapper = dialogWrapper;
    }
    public IAsyncCommand AddCommand => _addCommand ??= new AsyncCommand(OnAdd);

    public IAsyncCommand DeleteCommand => _deleteCommand ??= new AsyncCommand(() => OnDelete(SelectedValue), () => SelectedValue != null);

    public IAsyncCommand EditCommand => _editCommand ??= new AsyncCommand(() => OnEdit(SelectedValue), () => SelectedValue != null);

    public T SelectedValue
    {
        get => _selectedValue;
        set
        {
            SetProperty(ref _selectedValue, value);
            OnSelectedValueChanged();
        }
    }

    protected abstract Task OnDelete(T item);

    protected abstract Task OnEdit(T item);

    protected abstract Task OnAdd();

    protected virtual void OnSelectedValueChanged()
    {
        EditCommand.RaiseCanExecuteChanged();
        DeleteCommand.RaiseCanExecuteChanged();
    }
}
