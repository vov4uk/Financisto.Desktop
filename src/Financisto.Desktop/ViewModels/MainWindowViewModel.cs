using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Financisto.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Financisto.Desktop.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private string openBackupPath;
        private readonly ThemeService _themeService = AppServices.ThemeService;
        private readonly TransactionsService _transactionService = AppServices.TransactionsService;
        private readonly Dictionary<Type, ViewModelBase> _pages = new();
        [ObservableProperty]
        private ViewModelBase _currentPage;

        [ObservableProperty]
        private bool _isPaneOpen = true;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string? _statusMessage;

        [ObservableProperty]
        private ListItemTemplate? _selectedItemFundo;

        // Propriedades separadas para seleção dos menus
        [ObservableProperty]
        private ListItemTemplate? _selectedItemTopo;
        public MainWindowViewModel()
        {
            _ = AppServices.ThemeService;

            _currentPage = new DashboardPageViewModel(_transactionService);
            _pages[typeof(DashboardPageViewModel)] = _currentPage;
        }

        public string OpenBackupPath
        {
            get => openBackupPath;
            private set => SetProperty(ref openBackupPath, value);
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
            var instance = GetOrCreatePage(value);
            if (instance != null)
                CurrentPage = instance;
        }

        private ViewModelBase? GetOrCreatePage(ListItemTemplate value)
        {
            if (_pages.TryGetValue(value.ModelType, out var page))
                return page;

            var instance = CreatePageInstance(value);
            if (instance != null)
                _pages[value.ModelType] = instance;

            return instance;
        }

        private void RecreateAllPages()
        {
            _pages.Clear();

            foreach (var item in ItemsTop.Concat(ItemsBottom))
            {
                GetOrCreatePage(item);
            }
        }

        private ViewModelBase? CreatePageInstance(ListItemTemplate value)
        {
            object? instance;

            if (value.ModelType == typeof(ConfigurationsPageViewModel))
            {
                instance = Activator.CreateInstance(
                    value.ModelType,
                    _transactionService,
                    _themeService);
            }
            else if (value.ModelType == typeof(DashboardPageViewModel))
            {
                instance = Activator.CreateInstance(
                    value.ModelType,
                    _transactionService);
            }
            else if (value.ModelType == typeof(TransactionsPageViewModel))
            {
                instance = new TransactionsPageViewModel(AppServices.DatabaseService.CurrentDatabase);
            }
            else if (value.ModelType == typeof(AccountsPageViewModel))
            {
                instance = new AccountsPageViewModel(AppServices.DatabaseService.CurrentDatabase);
            }
            else
            {
                instance = Activator.CreateInstance(value.ModelType);
            }

            return instance as ViewModelBase;
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
        private async Task OpenBackup(Window window)
        {
            if (window == null || IsLoading) return;

            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Backup",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Backup files") { Patterns = new[] { "*.backup" } }
                }
            });

            var file = files.Count > 0 ? files[0] : null;
            if (file == null) return;

            await OpenBackupAsync(file.Path.LocalPath);
        }

        public async Task OpenBackupAsync(string path)
        {
            if (IsLoading) return;

            IsLoading = true;
            StatusMessage = null;
            try
            {
                var entitiesCount = await AppServices.DatabaseService.OpenBackupAsync(path);
                OpenBackupPath = path;

                RecreateAllPages();

                var transactionsItem = ItemsTop.First(x => x.ModelType == typeof(TransactionsPageViewModel));
                var wasAlreadySelected = ReferenceEquals(SelectedItemTopo, transactionsItem);

                SelectedItemFundo = null;
                SelectedItemTopo = transactionsItem;

                if (wasAlreadySelected)
                {
                    // Selection didn't change, so the usual navigation hook never fired.
                    NavigateToPage(transactionsItem);
                }

                StatusMessage = $"Loaded {entitiesCount} entities from backup";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open backup: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SaveBackup(Window window)
        {
            if (window == null || IsLoading) return;

            if (AppServices.DatabaseService.CurrentDatabase == null)
            {
                StatusMessage = "Open a backup first.";
                return;
            }

            var defaultName = Financisto.Adapter.BackupWriter.GenerateFileName();

            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Backup",
                SuggestedFileName = defaultName,
                DefaultExtension = "backup",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Backup files") { Patterns = new[] { "*.backup" } }
                }
            });

            if (file == null) return;

            IsLoading = true;
            StatusMessage = null;
            try
            {
                var path = file.Path.LocalPath;
                await AppServices.DatabaseService.SaveBackupAsync(path);
                StatusMessage = $"Backup saved to {path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save backup: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SaveBackupAsDb(Window window)
        {
            if (window == null || IsLoading) return;

            if (AppServices.DatabaseService.CurrentDatabase == null)
            {
                StatusMessage = "Open a backup first.";
                return;
            }

            var defaultName = Path.ChangeExtension(Financisto.Adapter.BackupWriter.GenerateFileName(), "db");

            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save as DB",
                SuggestedFileName = defaultName,
                DefaultExtension = "db",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("SQLite database") { Patterns = new[] { "*.db" } }
                }
            });

            if (file == null) return;

            IsLoading = true;
            StatusMessage = null;
            try
            {
                var path = file.Path.LocalPath;
                await AppServices.DatabaseService.SaveAsDbAsync(path);
                StatusMessage = $"Database saved to {path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save database: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
