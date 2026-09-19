using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using Financisto.Common.Entities;
using Financisto.Common.Model;

namespace Financisto.Converters
{
    public class DifferentCurrencyConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Count < 4
                || values[0] is not int fromAccountId
                || values[1] is not int toAccountId
                || values[2] is not int categoryId
                || values[3] is not AccountFilterModel monoAccount)
            {
                return false;
            }

            if (categoryId > 0)
            {
                return false;
            }

            var selectedAccount = DbManual.Account.FirstOrDefault(a => fromAccountId > 0 && a.Id == fromAccountId)
                                   ?? DbManual.Account.FirstOrDefault(a => toAccountId > 0 && a.Id == toAccountId);

            if (selectedAccount == null)
            {
                return false;
            }

            return selectedAccount.CurrencyId != monoAccount.CurrencyId;
        }
    }
}
