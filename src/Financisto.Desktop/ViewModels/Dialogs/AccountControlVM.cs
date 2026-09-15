using System.Collections.Generic;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.Desktop.Data;
using Prism.Commands;

namespace Financisto.Desktop.ViewModels.Dialogs
{
    public class AccountControlVM : DialogBaseVM
    {
        private AccountType selectedAccountType;
        private CardIssuer selectedCardIssuer;
        private ElectronicType selectedElectronicType;
        private CurrencyModel selectedCurrency;
        private DelegateCommand _clearTitleCommand;

        public AccountControlVM(AccountDto entity, bool isNew)
        {
            Entity = entity;
            IsNew = isNew;
            Currencies = DbManual.Currencies;

            InitSelections();

            entity.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(AccountDto.Title) or nameof(AccountDto.CurrencyId))
                    SaveCommand.NotifyCanExecuteChanged();
            };
        }

        public DelegateCommand ClearTitleCommand =>
            _clearTitleCommand ??= new DelegateCommand(() => { Entity.Title = default; SaveCommand.NotifyCanExecuteChanged(); });

        public AccountDto Entity { get; }

        public bool IsNew { get; }

        public List<CurrencyModel> Currencies { get; }

        public AccountType SelectedAccountType
        {
            get => selectedAccountType;
            set
            {
                selectedAccountType = value;
                Entity.Type = value.ToString();
                this.OnPropertyChanged(nameof(SelectedAccountType));
                this.OnPropertyChanged(nameof(ShowCardIssuer));
                this.OnPropertyChanged(nameof(ShowElectronicType));
                this.OnPropertyChanged(nameof(ShowIssuer));
                this.OnPropertyChanged(nameof(ShowNumber));
                this.OnPropertyChanged(nameof(ShowCreditCardFields));
            }
        }

        public CardIssuer SelectedCardIssuer
        {
            get => selectedCardIssuer;
            set
            {
                selectedCardIssuer = value;
                Entity.CardIssuer = value.ToString();
                OnPropertyChanged(nameof(SelectedCardIssuer));
            }
        }

        public ElectronicType SelectedElectronicType
        {
            get => selectedElectronicType;
            set
            {
                selectedElectronicType = value;
                Entity.CardIssuer = value.ToString();
                OnPropertyChanged(nameof(SelectedElectronicType));
            }
        }

        public CurrencyModel SelectedCurrency
        {
            get => selectedCurrency;
            set
            {
                selectedCurrency = value;
                if (value?.Id.HasValue == true)
                    Entity.CurrencyId = value.Id.Value;
                OnPropertyChanged(nameof(SelectedCurrency));
                SaveCommand.NotifyCanExecuteChanged();
            }
        }

        public bool ShowCardIssuer =>
            selectedAccountType is AccountType.DEBIT_CARD or AccountType.CREDIT_CARD;

        public bool ShowElectronicType =>
            selectedAccountType == AccountType.ELECTRONIC;

        public bool ShowIssuer =>
            selectedAccountType is AccountType.DEBIT_CARD or AccountType.CREDIT_CARD or AccountType.ELECTRONIC;

        public bool ShowNumber =>
            selectedAccountType is AccountType.DEBIT_CARD or AccountType.CREDIT_CARD;

        public bool ShowCreditCardFields =>
            selectedAccountType == AccountType.CREDIT_CARD;

        public override object OnRequestSave() => Entity;

        protected override bool CanSaveCommandExecute() =>
            !string.IsNullOrWhiteSpace(Entity?.Title) && Entity?.CurrencyId > 0;

        private void InitSelections()
        {
            if (!System.Enum.TryParse<AccountType>(Entity.Type, out var accountType))
                accountType = AccountType.CASH;
            selectedAccountType = accountType;
            Entity.Type = selectedAccountType.ToString();

            if (!string.IsNullOrEmpty(Entity.CardIssuer))
            {
                System.Enum.TryParse<CardIssuer>(Entity.CardIssuer, out selectedCardIssuer);
                System.Enum.TryParse<ElectronicType>(Entity.CardIssuer, out selectedElectronicType);
            }

            if (Entity.CurrencyId > 0)
                selectedCurrency = Currencies.Find(x => x.Id == Entity.CurrencyId);
        }
    }
}
