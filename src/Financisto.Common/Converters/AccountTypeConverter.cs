using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Financisto.Converters
{
    [ExcludeFromCodeCoverage]
    public class AccountTypeConverter : IMultiValueConverter
    {
        private static HashSet<string> KnownTypes = new HashSet<string> { "asset", "bank", "cash", "electronic", "liability" };
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            string type = null;
            string card_issuer = null;

            if (values.Count > 0)
                type = values[0]?.ToString()?.ToLowerInvariant();
            if (values.Count > 1)
                card_issuer = values[1]?.ToString()?.ToLowerInvariant();

            return LoadImage(GetImageUri(type, card_issuer));
        }

        private static IImage LoadImage(Uri uri)
        {
            try
            {
                using var stream = AssetLoader.Open(uri);
                return new Bitmap(stream);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Uri GetImageUri(string type, string card_issuer)
        {
            if (!string.IsNullOrEmpty(type) && type.Contains("card") && !string.IsNullOrEmpty(card_issuer))
            {
                return new Uri($"avares://Financisto.Common/Assets/Images/AccountType/account_type_card_{card_issuer}.png");
            }
            if (!string.IsNullOrEmpty(type) && KnownTypes.Contains(type))
            {
                return new Uri($"avares://Financisto.Common/Assets/Images/AccountType/account_type_{type}.png");
            }
            return new Uri("avares://Financisto.Common/Assets/Images/AccountType/account_type_other.png");
        }
    }
}
