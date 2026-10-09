using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Financisto.BankHelpers.Monobank
{
    /// <summary>Monobank's statement columns, in English and Ukrainian, mapped onto <see cref="BankTransaction"/>.</summary>
    internal sealed class MonobankMap : ClassMap<BankTransaction>
    {
        public MonobankMap()
        {
            Map(x => x.Date).Name("Date and time", "Дата i час операції").TypeConverter<DateTimeConvert>();
            Map(x => x.Description).Name("Description", "Деталі операції");
            Map(x => x.MCC).Name("MCC").Optional();
            Map(x => x.CardCurrencyAmount).Name("Card currency amount, (UAH)", "Card currency amount, (EUR)", "Card currency amount, (USD)", "Сума в валюті картки (UAH)", "Сума в валюті картки (EUR)", "Сума в валюті картки (USD)").TypeConverter<DoubleConverter>();
            Map(x => x.OperationAmount).Name("Operation amount", "Сума в валюті операції");
            Map(x => x.OperationCurrency).Name("Operation currency", "Валюта");
            Map(x => x.ExchangeRate).Name("Exchange rate", "Курс").TypeConverter<DoubleConvert>().Optional();
            Map(x => x.Commission).Name("Commission, (UAH)", "Commission, (EUR)", "Commission, (USD)", "Сума комісій (UAH)", "Сума комісій (EUR)", "Сума комісій (USD)").TypeConverter<DoubleConvert>().Optional();
            Map(x => x.Cashback).Name("Cashback amount, (UAH)", "Cashback amount, (EUR)", "Cashback amount, (USD)", "Сума кешбеку (UAH)", "Сума кешбеку (EUR)", "Сума кешбеку (USD)").TypeConverter<DoubleConvert>().Optional();
            Map(x => x.Balance).Name("Balance", "Залишок після операції").Optional();
        }
    }
}
