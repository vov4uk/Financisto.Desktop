using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Financisto.Common.Localization;
using Financisto.Desktop.Helpers;
using Prism.Commands;

namespace Financisto.Desktop.Wizards.Recipes
{
    public class Page2VM : RecipesWizardPageVMBase
    {
        private readonly RecipeCategoryMemory? memory;
        private DelegateCommand _addRowCommand;
        private DelegateCommand<FinancistoTransactionDto> _deleteCommand;
        private DelegateCommand _totalCommand;
        private DelegateCommand _clearAllNotesCommand;

        private ObservableCollection<FinancistoTransactionDto> financistoTransactions;

        /// <param name="memory">Gets the notes and categories of the rows before Clear notes wipes the notes; null = nothing is remembered.</param>
        public Page2VM(double totalAmount, RecipeCategoryMemory? memory = null)
        {
            this.memory = memory;
            TotalAmount = totalAmount;
            financistoTransactions = new();
        }

        public DelegateCommand AddRowCommand
        {
            get
            {
                return _addRowCommand ??= new DelegateCommand(() => { financistoTransactions.Add(new FinancistoTransactionDto() { Order = financistoTransactions.Count + 1 }); });
            }
        }

        public DelegateCommand<FinancistoTransactionDto> DeleteRowCommand
        {
            get
            {
                return _deleteCommand ??= new DelegateCommand<FinancistoTransactionDto>(tr =>
                {
                    financistoTransactions.Remove(tr);
                    for (int i = 0; i < financistoTransactions.Count; i++)
                    {
                        financistoTransactions[i].Order = i + 1;
                    }
                });
            }
        }

        public ObservableCollection<FinancistoTransactionDto> FinancistoTransactions
        {
            get => financistoTransactions;
            private set
            {
                financistoTransactions = value;
                RaisePropertyChanged(nameof(FinancistoTransactions));
            }
        }

        public override string Title => LocalizationService.Instance.recipe_wizard_transactions;

        public DelegateCommand TotalCommand
        {
            get
            {
                return _totalCommand ??= new DelegateCommand(CalculateFromAmounts);
            }
        }

        public DelegateCommand ClearAllNotesCommand
        {
            get
            {
                return _clearAllNotesCommand ??= new DelegateCommand(ClearAllNotes);
            }
        }

        public override bool IsValid() => true;
        public void SetTransactions(List<FinancistoTransactionDto> list)
        {
            FinancistoTransactions = new ObservableCollection<FinancistoTransactionDto>(list);
            CalculateFromAmounts();
        }

        private void CalculateFromAmounts()
        {
            base.CalculatedAmount =
                FinancistoTransactions.Sum(x => x.FromAmount) / 100.0;
        }

        private void ClearAllNotes()
        {
            // the notes are what a category is remembered by: take them before they go (the file is written when the wizard is saved)
            memory?.Remember(FinancistoTransactions);
            NLog.LogManager.GetCurrentClassLogger().Info($"[Recipes] notes cleared on {FinancistoTransactions.Count} rows");
            foreach (var item in FinancistoTransactions)
            {
                item.Note = null;
            }
        }
    }
}
