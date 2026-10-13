using System.Collections.Generic;
using System.Linq;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;

namespace Financisto.Desktop.Wizards.RecipesWizard.ViewModel
{
    /// <summary>
    /// Splits a receipt (recipe) into split parts: paste the OCR'ed text, check the parsed rows,
    /// and get <see cref="TransactionDto"/> sub-transactions for the transaction dialog.
    /// </summary>
    public class RecipesVM : WizardBaseVM
    {
        private readonly RecipeCategoryMemory memory;

        /// <param name="memory">The categories chosen on earlier receipts; by default the file next to the settings, read now.</param>
        public RecipesVM(double totalAmount, RecipeCategoryMemory? memory = null)
        {
            this.memory = memory ?? new RecipeCategoryMemory(RecipeCategoryMemory.DefaultPath);
            this.memory.Load();
            TotalAmount = totalAmount;
            Logger.Info($"[Recipes] wizard started, total {totalAmount:F2}, {this.memory.Categories.Count} remembered categories");
#pragma warning disable S1699 // Constructors should only call non-overridable methods
            CreatePages();
#pragma warning restore S1699 // Constructors should only call non-overridable methods
        }

        public double TotalAmount
        {
            get;
            private set;
        }

        public override void AfterCurrentPageUpdated(WizardPageBaseVM newValue)
        {
        }

        public override void BeforeCurrentPageUpdated(WizardPageBaseVM old, WizardPageBaseVM newValue)
        {
            if (old is Page1VM page1 && newValue is Page2VM)
            {
                page1.CalculateCurrentAmount();
                ((Page2VM)newValue).SetTransactions(page1.Amounts);
                Logger.Info($"[Recipes] page 1 -> 2: {page1.Amounts.Count} rows, calculated {page1.CalculatedAmount:F2} of {TotalAmount:F2}");
            }
        }

        public override void CreatePages()
        {
            var page1 = new Page1VM(this.TotalAmount, memory);
            page1.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(page1.Text))
                {
                    MoveNextCommand.RaiseCanExecuteChanged();
                }
            };

            _pages = new List<WizardPageBaseVM>()
            {
                page1,
                new Page2VM(this.TotalAmount, memory),
            }.AsReadOnly();

            CurrentPage = Pages[0];
        }
        public override object OnRequestClose(bool save)
        {
            if (save)
            {
                var rows = _pages.OfType<Page2VM>().Single().FinancistoTransactions;

                // what was chosen for these notes is kept for the next receipt (notes Clear notes already wiped were taken before)
                memory.Remember(rows);
                memory.Save();
                Logger.Info($"[Recipes] saved: {rows.Count} rows, {rows.Count(row => row.CategoryId > 0)} with a category, {rows.Count(row => !string.IsNullOrWhiteSpace(row.Note))} with a note");

                return rows.Select(TransformMonoTransaction).ToList();
            }
            return null;
        }

        private TransactionDto TransformMonoTransaction(FinancistoTransactionDto x)
        {
            var result = new TransactionDto
            {
                Id = 0,
                FromAmount = x.FromAmount,
                IsAmountNegative = x.FromAmount < 0,
                OriginalFromAmount = x.OriginalFromAmount ?? 0,
                OriginalCurrencyId = x.OriginalCurrencyId,
                Note = x.Note,
                LocationId = x.LocationId,
                ProjectId = x.ProjectId,
                CategoryId = x.CategoryId,
                Category = default,
            };

            return result;
        }
    }
}
