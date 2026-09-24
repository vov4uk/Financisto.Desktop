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
        private static HashSet<string> KnownTypes = new HashSet<string> { "asset", "bank", "cash", "liability", "electronic", "credit_card", "debit_card" };
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            string type = null;
            string card_issuer = null;

            if (values.Count > 0)
                type = values[0]?.ToString()?.ToLowerInvariant();
            if (values.Count > 1)
                card_issuer = values[1]?.ToString()?.ToLowerInvariant();

            // An issuer that doesn't belong to the type (e.g. DEBIT_CARD + GOOGLE_WALLET) has no icon: fall back to the type's icon.
            foreach (Uri uri in GetImageUris(type, card_issuer))
            {
                if (AssetLoader.Exists(uri))
                    return LoadImage(uri);
            }
            return null;
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

        private static IEnumerable<Uri> GetImageUris(string type, string card_issuer)
        {
            if (!string.IsNullOrEmpty(type) && card_issuer != "(unset)")
            {
                if (type.Contains("card") && !string.IsNullOrEmpty(card_issuer) && card_issuer != type)
                {
                    yield return new Uri($"avares://Financisto.Common/Assets/AccountType/account_type_card_{card_issuer}.png");
                }

                if (type.Contains("electronic") && !string.IsNullOrEmpty(card_issuer) && card_issuer != type)
                {
                    yield return new Uri($"avares://Financisto.Common/Assets/ElectronicType/electronic_type_{card_issuer}.png");
                }

                if (KnownTypes.Contains(type))
                {
                    if (type == "credit_card" || type == "debit_card")
                    {
                        yield return new Uri("avares://Financisto.Common/Assets/AccountType/account_type_card.png");
                    }
                    else
                    {
                        yield return new Uri($"avares://Financisto.Common/Assets/AccountType/account_type_{type}.png");
                    }
                }
            }

            yield return new Uri("avares://Financisto.Common/Assets/AccountType/account_type_other.png");
        }
    }
}
