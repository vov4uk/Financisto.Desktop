using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Financisto.Adapter;
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

    MainWindowVM ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();

        this.ViewModel = new MainWindowVM(new DialogWrapper(), new FinancistoDatabaseFactory(), new EntityReader(), new BackupWriter(), notificator, new BankHelperFactory(), new UpdateService());

        DataContext = ViewModel;
        var version = typeof(MainWindow).Assembly.GetName().Version;
        Title = $"Financisto Desktop v.{version}";
        Logger.Info("App started");
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
                    CheckForUpdatesOnStart = true
                }
            };
            SettingsService.Current.Save();
        }

        LocalizationService.Instance.ApplyLanguage(SettingsService.Current.Settings?.General.Language ?? Common.Localization.Language.English);
        var bakupFolder = SettingsService.Current.DefaultBackupDir ?? @$"C:\Users\{Environment.UserName}\Dropbox\apps\Financisto Holo";
        ViewModel.DefaultBackupDirectory = SettingsService.Current.DefaultBackupDir;

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
                await ViewModel.CheckForUpdateCommand.ExecuteAsync();
            }
        }
    }
}
