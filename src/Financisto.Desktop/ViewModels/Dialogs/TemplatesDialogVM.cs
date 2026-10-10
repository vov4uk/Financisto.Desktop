using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Financisto.Common;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Views.Dialogs;
using Prism.Commands;

namespace Financisto.Desktop.ViewModels.Dialogs;

/// <summary>What the template picker returns: the template to start from and Android's "xN" multiplier for its amounts.</summary>
public record TemplateSelection(int TemplateId, int Multiplier);

/// <summary>
/// The list of templates, as Android's two screens: <b>select</b> (<c>SelectTemplateFragment</c>: search by name or category,
/// the amount multiplier, "Edit templates") and <b>manage</b> (<c>TemplatesListFragment</c>: add, edit and delete templates).
/// Selecting returns a <see cref="TemplateSelection"/>; the page then opens the transaction or transfer dialog filled from it.
/// </summary>
public class TemplatesDialogVM : DialogBaseVM
{
    private readonly IFinancistoDatabase db;
    private readonly IDialogWrapper dialogWrapper;
    private readonly TemplateStore store;
    private readonly TransactionEditor editor;
    private List<BlotterModel> allTemplates = new();
    private ObservableCollection<BlotterModel> templates = new();
    private BlotterModel selectedTemplate;
    private string searchText = string.Empty;
    private int multiplier = 1;
    private DelegateCommand incrementMultiplierCommand;
    private DelegateCommand decrementMultiplierCommand;
    private Common.IAsyncCommand editTemplatesCommand;
    private Common.IAsyncCommand addTransactionCommand;
    private Common.IAsyncCommand addTransferCommand;
    private Common.IAsyncCommand editCommand;
    private Common.IAsyncCommand deleteCommand;

    private TemplatesDialogVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper, bool isManageMode)
    {
        this.db = db;
        this.dialogWrapper = dialogWrapper;
        store = new TemplateStore(db);
        editor = new TransactionEditor(db, dialogWrapper);
        IsManageMode = isManageMode;
    }

    /// <summary>The dialog with its templates already read.</summary>
    /// <param name="isManageMode">Add, edit and delete templates instead of picking one.</param>
    public static async Task<TemplatesDialogVM> CreateAsync(IFinancistoDatabase db, IDialogWrapper dialogWrapper, bool isManageMode = false)
    {
        var vm = new TemplatesDialogVM(db, dialogWrapper, isManageMode);
        await vm.LoadAsync();
        return vm;
    }

    public bool IsManageMode { get; }

    public bool IsSelectMode => !IsManageMode;

    /// <summary>The templates that match <see cref="SearchText"/>, newest first.</summary>
    public ObservableCollection<BlotterModel> Templates
    {
        get => templates;
        private set
        {
            if (SetProperty(ref templates, value))
            {
                OnPropertyChanged(nameof(HasNoTemplates));
            }
        }
    }

    public bool HasNoTemplates => Templates.Count == 0;

    public BlotterModel SelectedTemplate
    {
        get => selectedTemplate;
        set
        {
            if (SetProperty(ref selectedTemplate, value))
            {
                SaveCommand.NotifyCanExecuteChanged();
                editCommand?.RaiseCanExecuteChanged();
                deleteCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>Android filters by the template's name or its category.</summary>
    public string SearchText
    {
        get => searchText;
        set
        {
            if (SetProperty(ref searchText, value ?? string.Empty))
            {
                ApplyFilter();
            }
        }
    }

    public int Multiplier
    {
        get => multiplier;
        private set
        {
            if (SetProperty(ref multiplier, value))
            {
                OnPropertyChanged(nameof(MultiplierText));
            }
        }
    }

    public string MultiplierText => $"x{Multiplier}";

    /// <summary>What a double click does: pick the template, or edit it when managing the templates.</summary>
    public ICommand ActivateCommand => IsManageMode ? (ICommand)EditCommand : SaveCommand;

    public DelegateCommand IncrementMultiplierCommand => incrementMultiplierCommand ??= new DelegateCommand(() => Multiplier++);

    public DelegateCommand DecrementMultiplierCommand => decrementMultiplierCommand ??= new DelegateCommand(() => Multiplier = Math.Max(1, Multiplier - 1));

    /// <summary>Android's "Edit" button of the picker: the list where templates are added, edited and deleted.</summary>
    public Common.IAsyncCommand EditTemplatesCommand => editTemplatesCommand ??= new AsyncCommand(OnEditTemplates);

    public Common.IAsyncCommand AddTransactionCommand => addTransactionCommand ??= new AsyncCommand(() => OnAdd(isTransfer: false));

    public Common.IAsyncCommand AddTransferCommand => addTransferCommand ??= new AsyncCommand(() => OnAdd(isTransfer: true));

    public Common.IAsyncCommand EditCommand => editCommand ??= new AsyncCommand(OnEdit, () => SelectedTemplate != null);

    public Common.IAsyncCommand DeleteCommand => deleteCommand ??= new AsyncCommand(OnDelete, () => SelectedTemplate != null);

    public override object OnRequestSave() => new TemplateSelection(SelectedTemplate.Id, Multiplier);

    protected override bool CanSaveCommandExecute() => IsSelectMode && SelectedTemplate != null;

    /// <summary>Reads the templates again (after one was added, edited or deleted).</summary>
    public async Task LoadAsync()
    {
        allTemplates = await store.GetTemplatesAsync();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var text = SearchText.Trim();
        var selectedId = SelectedTemplate?.Id;

        IEnumerable<BlotterModel> matching = allTemplates;
        if (text.Length > 0)
        {
            matching = matching.Where(x => Contains(x.TemplateName, text) || Contains(x.CategoryTitle, text));
        }

        Templates = new ObservableCollection<BlotterModel>(matching);

        // Keep the selection while it still matches; while searching, the first match is the one Enter picks.
        SelectedTemplate = (selectedId == null ? null : Templates.FirstOrDefault(x => x.Id == selectedId))
            ?? (text.Length > 0 ? Templates.FirstOrDefault() : null);
    }

    private static bool Contains(string value, string text) =>
        value != null && value.Contains(text, StringComparison.CurrentCultureIgnoreCase);

    private async Task OnEditTemplates()
    {
        var manage = await CreateAsync(db, dialogWrapper, isManageMode: true);
        await dialogWrapper.ShowDialogAsync<TemplatesDialog>(manage, 560, 760, LocalizationService.Instance.transaction_templates);

        // the templates may have changed while the other list was open
        await LoadAsync();
    }

    private async Task OnAdd(bool isTransfer)
    {
        var template = await db.GetOrCreateTransactionAsync(0);
        template.IsTemplate = TemplateStore.TemplateFlag;

        var saved = isTransfer
            ? await editor.EditTransferAsync(template)
            : await editor.EditTransactionAsync(template, Array.Empty<Transaction>());
        if (saved)
        {
            await LoadAsync();
        }
    }

    private async Task OnEdit()
    {
        var template = await db.GetOrCreateTransactionAsync(SelectedTemplate.Id);
        if (template == null)
        {
            return;
        }

        var saved = TemplateStore.IsTransfer(template)
            ? await editor.EditTransferAsync(template)
            : await editor.EditTransactionAsync(template, await db.GetSubTransactionsAsync(template.Id));
        if (saved)
        {
            await LoadAsync();
        }
    }

    private async Task OnDelete()
    {
        if (await dialogWrapper.ShowMessageBoxAsync(LocalizationService.Instance.delete_template_confirm, LocalizationService.Instance.delete, true))
        {
            await store.DeleteTemplateAsync(SelectedTemplate.Id);
            await LoadAsync();
        }
    }
}
