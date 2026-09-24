using System.Collections.Generic;
using System.Linq;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.Desktop.Data;
using Prism.Commands;

namespace Financisto.Desktop.ViewModels.Dialogs
{
    public class AccountDialogVM : DialogBaseVM
    {
        private AccountType selectedAccountType;
        private CardIssuer selectedCardIssuer;
        private ElectronicType selectedElectronicType;
        private CurrencyModel selectedCurrency;
        private DelegateCommand _clearTitleCommand;

        public AccountDialogVM(AccountDto entity, bool isNew)
        {
            Entity = entity;
            IsNew = isNew;
            Currencies = DbManual.Currencies.Where(x => x.Id > 0).ToList();

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
                ApplyIssuerForType();
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
            ApplyIssuerForType();

            if (Entity.CurrencyId > 0)
                selectedCurrency = Currencies.Find(x => x.Id == Entity.CurrencyId);
        }

        // Same as Android AccountActivity.selectAccountType: card_issuer holds a card issuer for cards, an electronic
        // payment type for electronic accounts, and nothing otherwise. A mismatch (e.g. DEBIT_CARD + GOOGLE_WALLET)
        // makes Android's account list throw in CardIssuer.valueOf.
        private void ApplyIssuerForType()
        {
            if (selectedAccountType is AccountType.DEBIT_CARD or AccountType.CREDIT_CARD)
            {
                selectedCardIssuer = System.Enum.TryParse<CardIssuer>(Entity.CardIssuer, out var issuer) ? issuer : CardIssuer.DEFAULT;
                Entity.CardIssuer = selectedCardIssuer.ToString();
            }
            else if (selectedAccountType == AccountType.ELECTRONIC)
            {
                selectedElectronicType = System.Enum.TryParse<ElectronicType>(Entity.CardIssuer, out var type) ? type : ElectronicType.PAYPAL;
                Entity.CardIssuer = selectedElectronicType.ToString();
            }
            else
            {
                Entity.CardIssuer = null;
            }

            OnPropertyChanged(nameof(SelectedCardIssuer));
            OnPropertyChanged(nameof(SelectedElectronicType));
        }
    }
}
