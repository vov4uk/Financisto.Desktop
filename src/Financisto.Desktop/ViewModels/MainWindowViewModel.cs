using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Financisto.Desktop.Services;
using System;
using System.Collections.ObjectModel;

namespace Financisto.Desktop.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ThemeService _themeService = AppServices.ThemeService;
        private readonly TransactionsService _transacaoService = AppServices.TransactionsService;
        [ObservableProperty]
        private ViewModelBase _currentPage;

        [ObservableProperty]
        private bool _isPaneOpen = true;
        [ObservableProperty]
        private ListItemTemplate? _selectedItemFundo;

        // Propriedades separadas para seleção dos menus
        [ObservableProperty]
        private ListItemTemplate? _selectedItemTopo;
        public MainWindowViewModel()
        {
            _ = AppServices.ThemeService;

            _currentPage = new DashboardPageViewModel(_transacaoService);
        }

        public ObservableCollection<ListItemTemplate> ItemsBottom { get; } = new()
        {
            new(typeof(ConfigurationsPageViewModel), "Configurations", "settings_regular"),
        };

        public ObservableCollection<ListItemTemplate> ItemsTop { get; } = new()
        {
            new(typeof(DashboardPageViewModel), "Dashboard", "glance_regular"),
            new(typeof(AccountsPageViewModel), "Accounts", "inprivate_account_regular"),
            new(typeof(CategoriesPageViewModel), "Categories", "grid_regular"),
            new(typeof(ProjectsPageViewModel), "Projects", "grid_regular"),
            new(typeof(PayeesPageViewModel), "Payees", "money_regular"),
            new(typeof(LocationsPageViewModel), "Locations", "home_regular"),
            new(typeof(CurrenciesPageViewModel), "Currencies", "dark_theme_regular"),
            new(typeof(ExchangeRatesPageViewModel), "Exchange Rates", "arrow_sync_regular"),
            new(typeof(TransactionsPageViewModel), "Transactions", "money_regular"),
            new(typeof(ReportsPageViewModel), "Reports", "book_pulse_regular"),
            new(typeof(RulesPageViewModel), "Rules", "settings_regular"),
        };

        private void NavigateToPage(ListItemTemplate value)
        {
            object? instance;

            if (value.ModelType == typeof(ConfigurationsPageViewModel))
            {
                instance = Activator.CreateInstance(
                    value.ModelType,
                    _transacaoService,
                    _themeService);
            }
            else if (
                value.ModelType == typeof(DashboardPageViewModel) ||
                value.ModelType == typeof(TransactionsPageViewModel))
            {
                instance = Activator.CreateInstance(
                    value.ModelType,
                    _transacaoService);
            }
            else
            {
                instance = Activator.CreateInstance(value.ModelType);
            }

            if (instance is ViewModelBase vm)
                CurrentPage = vm;
        }

        partial void OnSelectedItemFundoChanged(ListItemTemplate? value)
        {
            if (value == null) return;

            // Limpa seleção do outro menu
            SelectedItemTopo = null;

            NavigateToPage(value);
        }

        partial void OnSelectedItemTopoChanged(ListItemTemplate? value)
        {
            if (value == null) return;

            // Limpa seleção do outro menu
            SelectedItemFundo = null;

            NavigateToPage(value);
        }
        [RelayCommand]
        private void OpenPane()
        {
            IsPaneOpen = !IsPaneOpen;
        }

        [RelayCommand]
        private void OpenBackup(Window window)
        {
            // TODO: not implemented yet.
        }
    }
}
