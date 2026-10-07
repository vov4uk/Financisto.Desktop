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
using Financisto.Converters;
using Financisto.DataAccess.Abstractions;
using Financisto.DataAccess.Data;
using Financisto.Desktop.Helpers;
using Financisto.Desktop.Services;
using Microsoft.EntityFrameworkCore;
using Avalonia.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Financisto.Desktop.ViewModels.Pages
{
    [ExcludeFromCodeCoverage]
    public class ExchangeRatesPageVM : EntityBaseVM<ExchangeRateModel>
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly IToastNotifierWrapper notifier;
        private IAsyncCommand _refreshExchangeRatesCommand;
        private string _from;
        private string _to;

        private List<ExchangeRateModel> _allRates = [];
        private ReportStructureSaldoRange _range = ReportStructureSaldoRange.Last12Months;
        private ISeries[] _series = [];
        private Axis[] _xAxes = [new DateTimeAxis(TimeSpan.FromDays(1), date => date.ToString("d"))];
        private Axis[] _yAxes = [new Axis()];

        public ExchangeRatesPageVM(IFinancistoDatabase db, IDialogWrapper dialogWrapper, IToastNotifierWrapper notifier)
            : base(db, dialogWrapper)
        {
            this.notifier = notifier;
            From = FromCurrencies.FirstOrDefault()!;
            To = ToCurrencies.FirstOrDefault()!;
        }

        /// <summary>Period selector shared with the saldo report.</summary>
        public ReportStructureSaldoRange Range
        {
            get => _range;
            set
            {
                if (SetProperty(ref _range, value))
                {
                    ApplyPeriod();
                }
            }
        }

        private DateTime? GetPeriodStart()
        {
            var today = DateTime.Today;
            var firstOfMonth = new DateTime(today.Year, today.Month, 1);
            return _range switch
            {
                ReportStructureSaldoRange.CurrentYear => new DateTime(today.Year, 1, 1),
                ReportStructureSaldoRange.Last6Months => firstOfMonth.AddMonths(-5),
                ReportStructureSaldoRange.Last12Months => firstOfMonth.AddMonths(-11),
                ReportStructureSaldoRange.Last2Years => new DateTime(today.Year - 1, 1, 1),
                ReportStructureSaldoRange.Last24Months => firstOfMonth.AddMonths(-23),
                ReportStructureSaldoRange.AllPeriods => null,
                _ => firstOfMonth.AddMonths(-11),
            };
        }

        public ISeries[] Series
        {
            get => _series;
            private set => SetProperty(ref _series, value);
        }

        public Axis[] XAxes
        {
            get => _xAxes;
            private set => SetProperty(ref _xAxes, value);
        }

        public Axis[] YAxes
        {
            get => _yAxes;
            private set => SetProperty(ref _yAxes, value);
        }

        public string From
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

        public static List<string> FromCurrencies => DbManual.Currencies.Where(x => x.Id > 0).Select(x => x.Name).ToList();

        public string To
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

        public List<string> ToCurrencies =>
            DbManual.Currencies.Where(x => x.Id > 0 && x.Name != _from).Select(x => x.Name).ToList();
        protected override Task OnAdd() => throw new NotImplementedException();

        protected override Task OnDelete(ExchangeRateModel item) => throw new NotImplementedException();

        protected override Task OnEdit(ExchangeRateModel item) => throw new NotImplementedException();

        protected override async Task RefreshData()
        {
            using var uow = db.CreateUnitOfWork();
            var currencyExchangeRepo = uow.GetRepository<CurrencyExchangeRate>();

            var fromId = DbManual.Currencies.Where(x => x.Id > 0).FirstOrDefault(x => x.Name == _from)?.Id!;
            var toId = DbManual.Currencies.Where(x => x.Id > 0).FirstOrDefault(x => x.Name == _to)?.Id!;

            var items = await currencyExchangeRepo.FindManyAndProjectAsync(
                x => x.FromCurrencyId == (fromId ?? 0) && x.ToCurrencyId == (toId ?? 0), // where
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
            _allRates = items.OrderBy(x => x.Date).ToList();
            ApplyPeriod();
        }

        /// <summary>Rebuilds the grid and the chart from the loaded rates, limited to the selected period.</summary>
        private void ApplyPeriod()
        {
            var start = GetPeriodStart();
            var fromMs = start.HasValue ? UnixTimeConverter.ConvertBack(start.Value) : long.MinValue;
            var rates = _allRates.Where(x => x.Date >= fromMs).ToList();

            var symbol = rates.FirstOrDefault()?.ToCurrency?.Symbol ?? string.Empty;
            ISeries[] series =
            [
                new LineSeries<DateTimePoint>
                {
                    Name = $"{_from} → {_to}",
                    Values = rates.Select(x => new DateTimePoint(UnixTimeConverter.Convert(x.Date), x.Rate)).ToArray(),
                    Fill = null,
                    LineSmoothness = 0,
                    Stroke = new SolidColorPaint(SKColor.Parse("#4E9A06"), 2),
                    GeometryFill = new SolidColorPaint(SKColors.White),
                    GeometryStroke = new SolidColorPaint(SKColor.Parse("#4E9A06"), 2),
                    GeometrySize = 6,
                },
            ];
            Axis[] yAxes = [new Axis { Labeler = value => $"{value:0.####} {symbol}" }];

            // navigation refreshes pages from a thread-pool thread; bound collections and charts must be updated on the UI thread
            void Apply()
            {
                Entities = new ObservableCollection<ExchangeRateModel>(rates.OrderByDescending(x => x.Date));
                Series = series;
                YAxes = yAxes;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Apply();
            }
            else
            {
                Dispatcher.UIThread.Post(Apply);
            }
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
                    case ExchangeRatesProviders.FloatRates:
                        exchangeRates = await exchangeRateLoader.LoadFloatRates();
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

                    notifier?.ShowMessage(string.Format(LocalizationService.Instance.exchange_rates_updated, erSettings.Provider.GetEnumDescription()));
                }
            }
            else
            {
                notifier?.ShowWarning(LocalizationService.Instance.exchange_rates_provider_not_configured);
            }
        }
    }
}
