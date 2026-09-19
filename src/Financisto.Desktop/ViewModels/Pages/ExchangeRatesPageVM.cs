using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Financisto.Common;
using Financisto.Common.Entities;
using Financisto.Common.Localization;
using Financisto.Common.Model;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Services;
using Microsoft.EntityFrameworkCore;
//using OxyPlot;
//using OxyPlot.Axes;
//using OxyPlot.Series;

namespace Financisto.Desktop.ViewModels.Pages
{
    [ExcludeFromCodeCoverage]
    public class ExchangeRatesPageVM : EntityBaseVM<ExchangeRateModel>
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly IToastNotifierWrapper notifier;
        private IAsyncCommand _refreshExchangeRatesCommand;
        private CurrencyModel _from;
        private CurrencyModel _to;

        //private PlotModel plotModel;

        public ExchangeRatesPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper, IToastNotifierWrapper notifier)
            : base(db, dialogWrapper)
        {
            this.notifier = notifier;
        }

        public CurrencyModel From
        {
            get => _from;
            set
            {
                if (SetProperty(ref _from, value))
                {
                    RaisePropertyChanged(nameof(From));
                    RaisePropertyChanged(nameof(ToCurrencies));
                }
            }
        }

        public static IEnumerable<CurrencyModel> FromCurrencies => DbManual.Currencies.Where(x => x.Id > 0);

        //public PlotModel PlotModel
        //{
        //    get => plotModel;
        //    private set
        //    {
        //        plotModel = value;
        //        RaisePropertyChanged(nameof(PlotModel));
        //    }
        //}

        public CurrencyModel To
        {
            get => _to;
            set
            {
                if (SetProperty(ref _to, value))
                {
                    RaisePropertyChanged(nameof(To));
                }
            }
        }

        public IAsyncCommand RefreshExchangeRatesCommand => _refreshExchangeRatesCommand ??= new AsyncCommand(RefreshExchangeRates_Click);

        public IEnumerable<CurrencyModel> ToCurrencies =>
            DbManual.Currencies.Where(x => x.Id > 0 && x.Id != _from?.Id);
        protected override Task OnAdd() => throw new NotImplementedException();

        protected override Task OnDelete(ExchangeRateModel item) => throw new NotImplementedException();

        protected override Task OnEdit(ExchangeRateModel item) => throw new NotImplementedException();

        protected override async Task RefreshData()
        {
            using var uow = db.CreateUnitOfWork();
            var currencyExchangeRepo = uow.GetRepository<CurrencyExchangeRate>();
            var items = await currencyExchangeRepo.FindManyAndProjectAsync(
                x => x.FromCurrencyId == (_from != null ? _from.Id : 0) && x.ToCurrencyId == (_to != null ? _to.Id : 0), // where
                rate => new ExchangeRateModel
                {
                    Date = rate.Date,
                    ToCurrencyId = rate.ToCurrencyId,
                    FromCurrencyId = rate.FromCurrencyId,
                    Rate = rate.Rate,
                    FromCurrency = new CurrencyModel
                    {
                        Id = rate.FromCurrency.Id,
                        Name = rate.FromCurrency.Name,
                        Symbol = rate.FromCurrency.Symbol,
                    },
                    ToCurrency = new CurrencyModel
                    {
                        Id = rate.ToCurrency.Id,
                        Name = rate.ToCurrency.Name,
                        Symbol = rate.ToCurrency.Symbol,
                    },
                }, // projection
                x => x.FromCurrency, // inclide
                x => x.ToCurrency // include
                );

            if (items == null)
            {
                return;
            }
            Entities = new ObservableCollection<ExchangeRateModel>(items.OrderByDescending(x => x.Date));

            //var model = new PlotModel();
            //var dateTimeAxis = new DateTimeAxis();

            //var linearAxis = new LinearAxis
            //{
            //    MajorGridlineStyle = LineStyle.Solid,
            //    MinorGridlineStyle = LineStyle.Dot,
            //};
            //var lineSeries = new LineSeries
            //{
            //    Color = OxyColor.FromArgb(255, 78, 154, 6),
            //    MarkerFill = OxyColor.FromArgb(255, 78, 154, 6),
            //    MarkerStroke = OxyColors.ForestGreen,
            //    MarkerType = MarkerType.Plus,
            //    StrokeThickness = 1
            //};

            //foreach (var item in items.OrderBy(x => x.Date))
            //{
            //    var date = UnixTimeConverter.Convert(item.Date);
            //    lineSeries.Points.Add(DateTimeAxis.CreateDataPoint(date, item.Rate));
            //}

            //model.Axes.Add(dateTimeAxis);
            //model.Axes.Add(linearAxis);
            //model.Series.Add(lineSeries);

            //PlotModel = model;

        }

        private async Task RefreshExchangeRates_Click()
        {
            var erSettings = SettingsService.Current.Settings.ExchangeRates;

            if (erSettings.Provider != ExchangeRatesProviders.None)
            {
                var exchangeRateLoader = new ExchangeRatesService();
                List<CurrencyExchangeRate> exchangeRates = new List<CurrencyExchangeRate>();

                switch (erSettings.Provider)
                {
                    case ExchangeRatesProviders.FreeCurrencyRates:
                        exchangeRates = await exchangeRateLoader.LoadFreeCurrencyRates();
                        break;
                    case ExchangeRatesProviders.OpenExchangeRates:
                        exchangeRates = await exchangeRateLoader.LoadOpenExchangeRates(erSettings.OpenExchangeRatesProviderAppId);
                        break;
                    case ExchangeRatesProviders.Monobank:
                        exchangeRates = await exchangeRateLoader.LoadMonobankRates();
                        break;
                }

                if (exchangeRates.Any())
                {
                    using var uow = db.CreateUnitOfWork();
                    var currencyExchangeRepo = uow.GetRepository<CurrencyExchangeRate>();
                    await currencyExchangeRepo.AddRangeAsync(exchangeRates);
                    try
                    {
                        await uow.SaveChangesAsync();
                        await RefreshData();
                    }
                    catch (DbUpdateException ex)
                    {
                        string msg = ex?.InnerException?.Message!;
                        if (!string.IsNullOrEmpty(msg) && msg.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
                        {
                            notifier?.ShowWarning(LocalizationService.Instance.exchange_rates_exist);
                        }
                        else
                        {
                            notifier?.ShowWarning(LocalizationService.Instance.exchange_rates_not_updated);
                        }
                        return;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error saving exchange rates to database.");
                        notifier?.ShowWarning(LocalizationService.Instance.exchange_rates_not_updated);
                        return;
                    }

                    notifier?.ShowMessage(string.Format(LocalizationService.Instance.exchange_rates_updated, erSettings.Provider));
                }
            }
            else
            {
                notifier?.ShowWarning(LocalizationService.Instance.exchange_rates_provider_not_configured);
            }
        }
    }
}
