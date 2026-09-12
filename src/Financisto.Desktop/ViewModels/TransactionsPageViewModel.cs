using Avalonia.Platform.Storage;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Financisto.Common.Entities;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Financisto.Desktop.ViewModels;

public partial class TransactionsPageViewModel : ViewModelBase
{
    private readonly IFinancistoDatabase _db;

    [ObservableProperty]
    private ObservableCollection<BlotterModel> _entities = new();

    [ObservableProperty]
    private bool _isLoading;

    public TransactionsPageViewModel(IFinancistoDatabase db)
    {
        _db = db;

        _ = RefreshDataAsync();
    }

    [RelayCommand]
    private async Task RefreshDataAsync()
    {
        if (_db == null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            using var uow = _db.CreateUnitOfWork();
            var repo = uow.GetRepository<BlotterTransactions>();

            var items = await repo.FindManyAndProjectAsync(
                predicate: x => true,
                projection: x => new BlotterModel
                {
                    Id = x.Id,
                    FromAccountId = x.FromAccountId,
                    FromAccountTitle = x.FromAccountTitle,
                    ToAccountId = x.ToAccountId,
                    ToAccountTitle = x.ToAccountTitle,
                    FromAccountCurrencyId = x.FromAccountCurrencyId,
                    CategoryId = x.CategoryId,
                    CategoryTitle = x.CategoryTitle,
                    LocationId = x.LocationId,
                    Project = x.ProjectId > 0 ? DbManual.ProjectIds.GetValueOrDefault(x.ProjectId.Value) : default,
                    Location = x.Location,
                    Payee = x.Payee,
                    Note = x.Note,
                    FromAmount = x.FromAmount,
                    ToAmount = x.ToAmount,
                    Datetime = x.DateTime,
                    OriginalCurrencyId = x.OriginalCurrencyId,
                    OriginalFromAmount = x.OriginalFromAmount,
                    FromAccountBalance = x.FromAccountBalance,
                    ToAccountBalance = x.ToAccountBalance,
                    FromAccountCurrency = DbManual.CurrencyIds.GetValueOrDefault(x.FromAccountCurrencyId),
                    ToAccountCurrency = x.ToAccountCurrency == null ? default : DbManual.CurrencyIds.GetValueOrDefault(x.ToAccountCurrencyId.Value),
                    OriginalCurrency = x.OriginalCurrency == null ? default : DbManual.CurrencyIds.GetValueOrDefault(x.OriginalCurrencyId.Value)
                });

            Entities = new ObservableCollection<BlotterModel>(items.OrderByDescending(x => x.Datetime));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ExportarExcelAsync()
    {
        try
        {
            var topLevel = App.Current?.ApplicationLifetime
                is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                    ? desktop.MainWindow
                    : null;

            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Salvar arquivo Excel",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Excel Workbook") { Patterns = new[] { "*.xlsx" } }
                },
                DefaultExtension = "xlsx"
            });

            if (file == null) return;

            var caminho = file.Path.LocalPath;

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Transactions");

            worksheet.Cell(1, 1).Value = "Date";
            worksheet.Cell(1, 2).Value = "Account";
            worksheet.Cell(1, 3).Value = "Category";
            worksheet.Cell(1, 4).Value = "Description";
            worksheet.Cell(1, 5).Value = "Amount";
            worksheet.Cell(1, 6).Value = "Balance";

            int row = 2;
            foreach (var t in Entities)
            {
                worksheet.Cell(row, 1).Value = Financisto.Converters.UnixTimeConverter.Convert(t.Datetime);
                worksheet.Cell(row, 2).Value = t.AccountTitle;
                worksheet.Cell(row, 3).Value = t.CategoryTitle;
                worksheet.Cell(row, 4).Value = t.TransactionTitle;
                worksheet.Cell(row, 5).Value = t.FromAmount / 100.0;
                worksheet.Cell(row, 6).Value = t.BalanceTitle;
                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(caminho);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao exportar Excel: " + ex.Message);
        }
    }
}
