using Avalonia.Controls;
using Avalonia.Interactivity;
using Financisto.Common.Localization;
using Financisto.Desktop.ViewModels;
using System;
using System.IO;
using System.Linq;

namespace Financisto.Desktop.Views;

public partial class MainWindow : Window
{
    private const string BackupSearchPattern = "*.backup";

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        LocalizationService.Instance.ApplyLanguage(Language.English);

        if (DataContext is not MainWindowViewModel viewModel) return;

        var backupFolder = $@"C:\Users\{Environment.UserName}\Dropbox\apps\Financisto Holo";

        if (!Directory.Exists(backupFolder)) return;

        var backupFile = Directory.EnumerateFiles(backupFolder, BackupSearchPattern)
            .OrderByDescending(x => x)
            .FirstOrDefault();

        if (!string.IsNullOrEmpty(backupFile) && File.Exists(backupFile))
        {
            await viewModel.OpenBackupAsync(backupFile);
        }
    }
}
