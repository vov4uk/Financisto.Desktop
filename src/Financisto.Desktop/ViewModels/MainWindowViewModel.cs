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
        private readonly TransacaoService _transacaoService = AppServices.TransacaoService;
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

        public ObservableCollection<ListItemTemplate> ItemsFundo { get; } = new()
        {
            new(typeof(ConfigurationsPageViewModel), "Configurations", "settings_regular"),
        };

        public ObservableCollection<ListItemTemplate> ItemsTopo { get; } = new()
        {
            new(typeof(DashboardPageViewModel), "Dashboard", "glance_regular"),
            new(typeof(TransacaoPageViewModel), "Transactions", "money_regular"),
            new(typeof(CategoriasPageViewModel), "Categories", "grid_regular"),
            new(typeof(RelatorioPageViewModel), "Reports", "book_pulse_regular"),
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
                value.ModelType == typeof(TransacaoPageViewModel))
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
    }
}
