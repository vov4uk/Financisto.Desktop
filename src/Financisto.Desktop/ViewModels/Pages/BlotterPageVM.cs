using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.Common.Utils;
using Financisto.Converters;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.DataAccess.Utils;
using Financisto.DataAccess.View;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.ViewModels.Dialogs;
using Financisto.Desktop.Views.Dialogs;

namespace Financisto.Desktop.ViewModels.Pages
{
    public class BlotterPageVM : EntityBaseVM<BlotterModel>
    {
        private IAsyncCommand _templateCommand;
        private IAsyncCommand _saveAsTemplateCommand;
        private IAsyncCommand _addTransferCommand;
        private IAsyncCommand _duplicateCommand;
        private IAsyncCommand _clearFiltersCommand;
        private IAsyncCommand _infoCommand;
        private IAsyncCommand<IList> _selectionChangedCommand;
        private string _selectionSummary;
        private DateTime? _from;
        private DateTime? _to;
        private PeriodType _periodType;
        private ObservableCollection<AccountFilterModel> _selectedAccounts = new ObservableCollection<AccountFilterModel>();
        private CategoryModel _category;
        private PayeeModel _payee;
        private ProjectModel _project;
        private LocationModel _location;
        private ObservableCollection<TagModel> _tags = new ObservableCollection<TagModel>();
        private TransactionEditor _editor;
        private TemplateStore _templates;

        public BlotterPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper)
            : base(db, dialogWrapper)
        {
        }

        public DateTime? From
        {
            get => _from;
            set
            {
                if (SetProperty(ref _from, value))
                {
                    RaisePropertyChanged(nameof(From));
                }
            }
        }

        public DateTime? To
        {
            get => _to;
            set
            {
                if (SetProperty(ref _to, value))
                {
                    RaisePropertyChanged(nameof(To));
                }
            }
        }

        public PeriodType PeriodType
        {
            get => _periodType;
            set
            {
                if (SetProperty(ref _periodType, value))
                {
                    RaisePropertyChanged(nameof(PeriodType));
                }
            }
        }

        public ObservableCollection<AccountFilterModel> SelectedAccounts
        {
            get => _selectedAccounts;
            set
            {
                _selectedAccounts = value ?? new ObservableCollection<AccountFilterModel>();
                RaisePropertyChanged(nameof(SelectedAccounts));
            }
        }

        private TransactionEditor Editor => _editor ??= new TransactionEditor(db, dialogWrapper);

        private TemplateStore Templates => _templates ??= new TemplateStore(db);

        /// <summary>Where "Save as template" reports success (Android's toast); set by <c>MainWindowVM</c>, nothing is shown without it.</summary>
        public IToastNotifierWrapper Notifier { get; set; }

        // the account new transactions default to: only when exactly one account is filtered
        private int? SingleAccountId => SelectedAccounts.Count == 1 ? SelectedAccounts[0]?.Id : null;

        public CategoryModel Category
        {
            get => _category ??= DbManual.Category.Find(p => !p.Id.HasValue)!;
            set
            {
                _category = value;
                RaisePropertyChanged(nameof(Category));
            }
        }

        public PayeeModel Payee
        {
            get => _payee ??= DbManual.Payee.Find(p => !p.Id.HasValue)!;
            set
            {
                _payee = value;
                RaisePropertyChanged(nameof(Payee));
            }
        }

        public ProjectModel Project
        {
            get => _project ??= DbManual.Project.Find(p => !p.Id.HasValue)!;
            set
            {
                _project = value;
                RaisePropertyChanged(nameof(Project));
            }
        }

        public LocationModel Location
        {
            get => _location ??= DbManual.Location.Find(p => !p.Id.HasValue)!;
            set
            {
                _location = value;
                RaisePropertyChanged(nameof(Location));
            }
        }

        public ObservableCollection<TagModel> Tags
        {
            get => _tags;
            set
            {
                _tags = value ?? new ObservableCollection<TagModel>();
                RaisePropertyChanged(nameof(Tags));
            }
        }

        /// <summary>Android's blotter "Template" button: pick a template, then the transaction or transfer dialog opens filled from it.</summary>
        public IAsyncCommand TemplateCommand => _templateCommand ??= new AsyncCommand(OnTemplate);

        /// <summary>Android's "Save as template" for the selected row.</summary>
        public IAsyncCommand SaveAsTemplateCommand => _saveAsTemplateCommand ??= new AsyncCommand(() => OnSaveAsTemplate(SelectedValue), () => SelectedValue != null);

        public IAsyncCommand AddTransferCommand => _addTransferCommand ??= new AsyncCommand(AddTransfer);

        public IAsyncCommand DuplicateCommand => _duplicateCommand ??= new AsyncCommand(() => OnDuplicate(SelectedValue), () => SelectedValue != null);

        public IAsyncCommand ClearFiltersCommand => _clearFiltersCommand ??= new AsyncCommand(ClearFilters);

        public IAsyncCommand InfoCommand => _infoCommand ??= new AsyncCommand(() => Task.CompletedTask, () => false);

        public string SelectionSummary
        {
            get => _selectionSummary;
            private set => SetProperty(ref _selectionSummary, value);
        }

        public IAsyncCommand<IList> SelectionChangedCommand =>
            _selectionChangedCommand ??= new AsyncCommand<IList>(OnSelectionChanged);

        private Task OnSelectionChanged(IList selectedItems)
        {
            var selectedRows = (selectedItems ?? Array.Empty<object>())
                .Cast<object>()
                .OfType<BlotterModel>()
                .Distinct()
                .ToList();

            if (selectedRows.Count < 2)
            {
                SelectionSummary = string.Empty;
                return Task.CompletedTask;
            }

            var totalsByCurrency = selectedRows
                .Where(item => item.ToAccountId == null || item.ToAccountId == 0)
                .GroupBy(item => item.FromAccountCurrency)
                .Select(g => BlotterUtils.SetAmountText(g.Key, g.Sum(i => i.FromAmount), true));

            SelectionSummary = string.Join("   ", totalsByCurrency);
            return Task.CompletedTask;
        }

        private async Task ClearFilters()
        {
            ResetFilters();
            await RefreshDataCommand.ExecuteAsync();
        }

        /// <summary>Android's account "Blotter" command: only this account's transactions. The page refreshes when it is navigated to.</summary>
        internal void ShowAccount(AccountFilterModel account)
        {
            ResetFilters();
            SelectedAccounts = new ObservableCollection<AccountFilterModel> { account };
        }

        private void ResetFilters()
        {
            PeriodType = PeriodType.AllTime;
            // through the properties, so the period filter's date pickers clear too, also when the type was already AllTime
            From = null;
            To = null;
            SelectedAccounts = new ObservableCollection<AccountFilterModel>();
            Category = default!;
            Payee = default!;
            Project = default!;
            Location = default!;
            Tags = new ObservableCollection<TagModel>();
        }

        protected override async Task OnDelete(BlotterModel item)
        {
            // Like Android's BlotterOperations, a part of a split stands for its parent: that is what gets deleted (after saying so).
            var message = item.IsSplitPart ? LocalizationService.Instance.delete_transaction_parent_confirm : LocalizationService.Instance.confirm_delete_transaction;
            if (await this.dialogWrapper.ShowMessageBoxAsync(message, LocalizationService.Instance.delete, true))
            {
                var targetId = TargetId(item);
                var parent = item.IsSplitPart ? await db.GetOrCreateTransactionAsync(targetId) : null;
                var subTransactions = await db.GetSubTransactionsAsync(targetId);
                await Editor.DeleteTransactionAsync(targetId);

                // An incoming split transfer has the other account on its "from" side.
                var accounts = subTransactions.SelectMany(x => new[] { x.FromAccountId, x.ToAccountId })
                    .Append(item.FromAccountId)
                    .Append(item.ToAccountId ?? 0)
                    .Append(parent?.FromAccountId ?? 0)
                    .Append(parent?.ToAccountId ?? 0)
                    .Where(x => x > 0)
                    .Distinct()
                    .ToList();
                foreach (var account in accounts)
                {
                    await db.RebuildAccountBalanceAsync(account);
                }
                await RefreshData();
            }
        }

        protected override async Task OnEdit(BlotterModel item)
        {
            if (item.Type == "Transfer" && !item.IsSplitPart)
            {
                var transfer = await GetTransfer(item.Id);
                await EditTransferAsync(transfer);
            }
            else
            {
                // A part of a split opens its parent, which is never a transfer.
                var t = await GetTransaction(TargetId(item));
                await EditTransactionAsync(t.transaction, t.subTransactions);
            }
        }

        /// <summary>The transaction an operation on a blotter row is about: the parent for a part of a split, else the row itself.</summary>
        private static int TargetId(BlotterModel item) => item.IsSplitPart ? item.ParentId : item.Id;

        private async Task AddTransfer()
        {
            Transaction transfer = await db.GetOrCreateTransactionAsync(0);
            if (SingleAccountId != null)
            {
                transfer.FromAccountId = SingleAccountId.Value;
            }
            await EditTransferAsync(transfer);
        }

        private async Task OnDuplicate(BlotterModel item)
        {
            if (item.Type == "Transfer" && !item.IsSplitPart)
            {
                var transfer = await GetTransfer(item.Id, true);
                await EditTransferAsync(transfer);
            }
            else
            {
                var t = await GetTransaction(TargetId(item), true);
                await EditTransactionAsync(t.transaction, t.subTransactions);
            }
        }

        private async Task OnTemplate()
        {
            var dialog = await TemplatesDialogVM.CreateAsync(db, dialogWrapper);
            var result = await dialogWrapper.ShowDialogAsync<TemplatesDialog>(dialog, 560, 760, LocalizationService.Instance.transaction_templates);
            if (result is not TemplateSelection selection)
            {
                return;
            }

            var created = await Templates.CreateFromTemplateAsync(selection.TemplateId, selection.Multiplier);
            if (created is not { } copy)
            {
                return;
            }

            // Nothing is stored until the dialog is saved, so cancelling leaves no half-made transaction behind.
            if (TemplateStore.IsTransfer(copy.Transaction))
            {
                await EditTransferAsync(copy.Transaction);
            }
            else
            {
                await EditTransactionAsync(copy.Transaction, copy.SubTransactions);
            }
        }

        private async Task OnSaveAsTemplate(BlotterModel item)
        {
            // like Android's BlotterOperations, a part of a split stands for its parent
            if (await Templates.SaveAsTemplateAsync(TargetId(item)) > 0)
            {
                Notifier?.ShowMessage(LocalizationService.Instance.save_as_template_success);
            }
        }

        protected override void OnSelectedValueChanged()
        {
            DuplicateCommand.RaiseCanExecuteChanged();
            SaveAsTemplateCommand.RaiseCanExecuteChanged();
            base.OnSelectedValueChanged();
        }

        protected override async Task OnAdd()
        {
            Transaction transaction = await db.GetOrCreateTransactionAsync(0);
            IEnumerable<Transaction> subTransactions = await db.GetSubTransactionsAsync(0);

            if (SingleAccountId != null)
            {
                transaction.FromAccountId = SingleAccountId.Value;
            }

            await EditTransactionAsync(transaction, subTransactions);
        }

        private async Task EditTransferAsync(Transaction transfer)
        {
            if (await Editor.EditTransferAsync(transfer))
            {
                await RefreshData();
            }
        }

        private async Task EditTransactionAsync(Transaction transaction, IEnumerable<Transaction> subTransactions)
        {
            if (await Editor.EditTransactionAsync(transaction, subTransactions))
            {
                await RefreshData();
            }
        }

        private async Task<Transaction> GetTransfer(int id, bool isDuplicate = false)
        {
            Transaction transfer = await db.GetOrCreateTransactionAsync(id);
            if (isDuplicate)
            {
                transfer.Id = 0;
                transfer.DateTime = UnixTimeConverter.ConvertBack(DateTime.Now);
            }

            return transfer;
        }

        private async Task<(Transaction transaction, IEnumerable<Transaction> subTransactions)> GetTransaction(int id, bool isDuplicate = false)
        {
            Transaction transaction = await db.GetOrCreateTransactionAsync(id);
            IEnumerable<Transaction> subTransactions = await db.GetSubTransactionsAsync(id);

            if (isDuplicate)
            {
                transaction.Id = 0;
                transaction.DateTime = UnixTimeConverter.ConvertBack(DateTime.Now);

                // The copy gets its own split parts; keeping the original ids would re-parent the original's parts.
                foreach (var subTransaction in subTransactions)
                {
                    subTransaction.Id = 0;
                }
            }

            return (transaction, subTransactions);
        }

        protected override async Task RefreshData()
        {
            var accountIds = SelectedAccounts.Where(a => a?.Id != null).Select(a => a.Id.Value).ToList();

            List<BlotterModel> items;
            if (accountIds.Count > 0)
            {
                // Android's account blotter (getBlotterForAccount, v_blotter_for_account_with_splits): every row is seen from one of the accounts,
                // so a transfer into an account is a row of its own, and so is a part of another account's split that moved money into it.
                // A transaction still shows once: a split part is left out when its parent's account is shown (the parent stands for it),
                // and the "to" side of a transfer is left out when the account it came from is shown too.
                var predicate = BuildFilter<BlotterTransactionsForAccountWithSplits>().And(
                    x => accountIds.Contains(x.FromAccountId)
                        && (x.ParentId == 0 || !accountIds.Contains(x.ParentAccountId))
                        && !(x.IsTransfer == -1 && x.ToAccountId != null && accountIds.Contains(x.ToAccountId.Value)));
                items = await QueryAsync(db, predicate);
                items?.ForEach(x => x.IsAccountPerspective = true);
            }
            else
            {
                items = await QueryAsync(db, BuildFilter<BlotterTransactions>());
            }

            if (items != null)
            {
                Entities = new ObservableCollection<BlotterModel>(items.OrderByDescending(x => x.Datetime).ThenByDescending(x => x.Id));
            }
        }

        /// <summary>The period, category, project, payee, location and tag filters, for either of the blotter views.</summary>
        private Expression<Func<T, bool>> BuildFilter<T>()
            where T : TransactionsView
        {
            var fromUnix = UnixTimeConverter.ConvertBack(From ?? DateTime.MinValue.ToLocalTime());
            var toUnix = UnixTimeConverter.ConvertBack(To ?? DateTime.MaxValue.ToLocalTime());

            Expression<Func<T, bool>> predicate = x => x.DateTime >= fromUnix && x.DateTime <= toUnix;

            if (Category?.Id != null)
            {
                predicate = predicate.And(x => x.CategoryId == _category.Id && x.ToAccountId == null);
            }

            if (Project?.Id != null)
            {
                predicate = predicate.And(x => x.ProjectId == _project.Id);
            }

            if (Payee?.Id != null)
            {
                predicate = predicate.And(x => x.PayeeId == _payee.Id);
            }

            if (Location?.Id != null)
            {
                predicate = predicate.And(x => x.LocationId == _location.Id);
            }

            var tagTitles = Tags.Where(t => !string.IsNullOrWhiteSpace(t?.Title)).Select(t => t.Title).ToList();
            if (tagTitles.Count > 0)
            {
                Expression<Func<T, bool>> tagsPredicate = null;
                foreach (var title in tagTitles)
                {
                    Expression<Func<T, bool>> hasTag = x => x.Tags != null && x.Tags.Contains(title);
                    tagsPredicate = tagsPredicate == null ? hasTag : tagsPredicate.Or(hasTag);
                }

                predicate = predicate.And(tagsPredicate);
            }

            return predicate;
        }

        /// <summary>Blotter rows (the <c>v_blotter</c> view) matching <paramref name="predicate"/>, unordered.</summary>
        internal static Task<List<BlotterModel>> QueryAsync(IFinancistoDatabase db, Expression<Func<BlotterTransactions, bool>> predicate) =>
            QueryAsync<BlotterTransactions>(db, predicate);

        internal static async Task<List<BlotterModel>> QueryAsync<T>(IFinancistoDatabase db, Expression<Func<T, bool>> predicate)
            where T : TransactionsView
        {
            using var uow = db.CreateUnitOfWork();
            var repo = uow.GetRepository<T>();
            return await repo.FindManyAndProjectAsync(
                predicate: predicate,
                projection: x => new BlotterModel
                {
                    Id = x.Id,
                    ParentId = x.ParentId,
                    IsTransfer = x.IsTransfer,
                    FromAccountId  = x.FromAccountId,
                    FromAccountTitle = x.FromAccountTitle,
                    ToAccountId = x.ToAccountId,
                    ToAccountTitle = x.ToAccountTitle,
                    FromAccountCurrencyId = x.FromAccountCurrencyId,
                    CategoryId = x.CategoryId,
                    CategoryTitle = x.CategoryTitle,
                    LocationId = x.LocationId,
                    Project = x.ProjectId > 0 ? DbManual.ProjectIds.GetValueOrDefault(x.ProjectId.Value) : default,
                    Location = x.Location,
                    Payee = x.Payee,
                    Tags = x.Tags,
                    TemplateName = x.TemplateName,
                    Note = x.Note,
                    FromAmount = x.FromAmount,
                    ToAmount = x.ToAmount,
                    Datetime = x.DateTime,
                    OriginalCurrencyId = x.OriginalCurrencyId,
                    OriginalFromAmount = x.OriginalFromAmount,
                    FromAccountBalance = x.FromAccountBalance,
                    ToAccountBalance = x.ToAccountBalance,
                    FromAccountCurrency = DbManual.CurrencyIds.GetValueOrDefault(x.FromAccountCurrencyId),
                    ToAccountCurrency = x.ToAccountCurrencyId == null ? default : DbManual.CurrencyIds.GetValueOrDefault(x.ToAccountCurrencyId.Value),
                    OriginalCurrency = x.OriginalCurrencyId == null ? default : DbManual.CurrencyIds.GetValueOrDefault(x.OriginalCurrencyId.Value)
                });
        }
    }
}
