using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Financisto.Adapter;
using Financisto.BankHelpers;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.DataAccess;
using Financisto.Desktop.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Helpers.BankHelper;
using Financisto.Desktop.Services;
using Financisto.Desktop.ViewModels;

namespace Financisto.Desktop.Views;

public partial class MainWindow : Window
{
    private const string BackupFormat = "*.backup";
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private readonly ToastNotifierWrapper notificator = new ToastNotifierWrapper();

    private IBankHelperProvider? bankHelperProvider;

    MainWindowVM ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();

        this.ViewModel = new MainWindowVM(new DialogWrapper(), new FinancistoDatabaseFactory(), new EntityReader(), new BackupWriter(), notificator, new UpdateService());

        DataContext = ViewModel;

        // The Import menu stays disabled until the plugins are loaded; loading them is slow enough to keep off the UI thread.
        ImportMenu.IsEnabled = false;
        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
        Closed += (_, _) => LocalizationService.Instance.PropertyChanged -= OnLocalizationChanged;
        _ = LoadBankHelpersAsync();

        var version = typeof(MainWindow).Assembly.GetName().Version;
        Title = $"Financisto Desktop v.{version?.ToString(3)}";
        Logger.Info("App started");
    }

    private async Task LoadBankHelpersAsync()
    {
        try
        {
            var pluginsPath = StartOptions.Current.PluginsPath;
            bankHelperProvider = await Task.Run(() => new PluginBankHelperProvider(pluginsPath));
            PopulateImportMenu();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "The bank helper plugins could not be loaded");
        }
    }

    // The titles depend on the UI language, so the menu (and its alphabetical order) is rebuilt when the language changes.
    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LocalizationService.CurrentCulture))
        {
            Dispatcher.UIThread.Post(PopulateImportMenu);
        }
    }

    /// <summary>Fills the Import menu from the bank helper plugins: one entry per helper, a separator between statement formats.</summary>
    private void PopulateImportMenu()
    {
        if (bankHelperProvider is null)
        {
            return;
        }

        var banks = bankHelperProvider.BankHelpers
                    .Select(x => new BankImportItem(x))
                    .GroupBy(x => x.Helper.ReportType)
                    .OrderBy(x => x.Key)
                    .Select(x => (IReadOnlyList<BankImportItem>)x.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase).ToList())
                    .ToList();

        var entries = new List<Control>();
        foreach (var group in banks)
        {
            if (entries.Count > 0)
            {
                entries.Add(new Separator());
            }

            entries.AddRange(group.Select(CreateImportMenuItem));
        }

        ImportMenu.ItemsSource = entries;
        ImportMenu.IsEnabled = entries.Count > 0;
    }

    private MenuItem CreateImportMenuItem(BankImportItem item)
    {
        var menuItem = new MenuItem
        {
            Classes = { "bankImport" },
            Command = (ICommand)ViewModel.ImportCommand,
            CommandParameter = item.Helper,
            Header = item.Title,
            Tag = item.ReportTypeLabel,
            Icon = CreateIcon(item),
        };
        return menuItem;
    }

    private static Image? CreateIcon(BankImportItem item)
    {
        try
        {
            var bytes = item.Icon;
            if (bytes is { Length: > 0 })
            {
                return new Image { Source = new Bitmap(new MemoryStream(bytes)) };
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, $"The icon of {item.Helper.GetType().Name} could not be read");
        }

        return null;
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            SettingsService.Current.Load();
            ArgumentNullException.ThrowIfNull(SettingsService.Current.Settings);
            ArgumentNullException.ThrowIfNull(SettingsService.Current.Settings.ExchangeRates);
            ArgumentNullException.ThrowIfNull(SettingsService.Current.Settings.General);
        }
        catch
        {
            notificator.ShowWarning(LocalizationService.Instance.settings_corrupted);
            SettingsService.Current.Settings = new SettingsDto()
            {
                ExchangeRates = new SettingsExchangeRates
                {
                    Provider = ExchangeRatesProviders.None,
                    UpdateOnStart = false,
                },
                General = new SettingsGeneralDto
                {
                    CheckForUpdatesOnStart = true,
                    Language = Language.English,
                    CurrentAppTheme = AppThemeType.System,
                }
            };
            SettingsService.Current.Save();
        }

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = SettingsService.Current.Settings?.General.ThemeVariant;
        }

        LocalizationService.Instance.ApplyLanguage(SettingsService.Current.Settings?.General.Language ?? Language.English);
        var bakupFolder = !string.IsNullOrEmpty(SettingsService.Current.Settings?.General.DefaultBackupDir) ? SettingsService.Current.Settings.General.DefaultBackupDir : @$"C:\Users\{Environment.UserName}\Dropbox\apps\Financisto Holo";

        if (Directory.Exists(bakupFolder))
        {
            var backupFile = Directory.EnumerateFiles(bakupFolder, BackupFormat).OrderByDescending(x => x).FirstOrDefault();
            if (!string.IsNullOrEmpty(backupFile) && File.Exists(backupFile))
            {
                Logger.Info($"Automatically loaded backup : {backupFile}");
                await Task.Run(() => ViewModel.OpenBackup(backupFile));
            }

            if (SettingsService.Current.Settings?.General.CheckForUpdatesOnStart == true)
            {
                await Task.Run(() => ViewModel.CheckForUpdatesAsync());
            }
        }
    }
}
