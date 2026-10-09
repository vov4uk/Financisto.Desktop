using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Financisto.Common.Entities;
using Financisto.Common.Utils;

namespace Financisto.Converters
{
    [ExcludeFromCodeCoverage]
    public class AccountTypeConverter : IMultiValueConverter
    {
        private static HashSet<string> KnownTypes = new HashSet<string> { "asset", "bank", "cash", "liability", "electronic", "credit_card", "debit_card" };

        /// <summary>Values: account type, card issuer / electronic type, and (optionally) the <see cref="IconSetType"/>. The last one is also what makes bindings refresh when the setting changes.</summary>
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            string type = null;
            string card_issuer = null;
            IconSetType iconSet = values.Count > 2 && values[2] is IconSetType set ? set : IconSettings.Instance.IconSet;

            if (values.Count > 0)
                type = values[0]?.ToString()?.ToLowerInvariant();
            if (values.Count > 1)
                card_issuer = values[1]?.ToString()?.ToLowerInvariant();

            // An issuer that doesn't belong to the type (e.g. DEBIT_CARD + GOOGLE_WALLET) has no icon: fall back to the type's icon.
            foreach (Uri uri in GetImageUris(type, card_issuer, iconSet))
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
                if (uri.AbsolutePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    return LoadSvg(stream);
                }

                return new Bitmap(stream);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static IImage LoadSvg(System.IO.Stream stream)
        {
            const int size = 128;
            using var svg = new Svg.Skia.SKSvg();
            if (svg.Load(stream) is not { } picture)
            {
                return null;
            }

            var bounds = picture.CullRect;
            float scale = Math.Min(size / bounds.Width, size / bounds.Height);
            using var bitmap = new SkiaSharp.SKBitmap(size, size);
            using (var canvas = new SkiaSharp.SKCanvas(bitmap))
            {
                canvas.Clear(SkiaSharp.SKColors.Transparent);
                canvas.Translate((size - bounds.Width * scale) / 2 - bounds.Left * scale, (size - bounds.Height * scale) / 2 - bounds.Top * scale);
                canvas.Scale(scale);
                canvas.DrawPicture(picture);
            }

            using var data = SkiaSharp.SKImage.FromBitmap(bitmap).Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var png = new System.IO.MemoryStream(data.ToArray());
            return new Bitmap(png);
        }

        private static IEnumerable<Uri> GetImageUris(string type, string card_issuer, IconSetType iconSet)
        {
            if (!string.IsNullOrEmpty(type) && card_issuer != "(unset)")
            {
                if (type.Contains("card") && !string.IsNullOrEmpty(card_issuer) && card_issuer != type)
                {
                    foreach (var uri in WithFallback($"avares://Financisto.Common/Assets/AccountType/account_type_card_{card_issuer}", iconSet)) yield return uri;
                }

                if (type.Contains("electronic") && !string.IsNullOrEmpty(card_issuer) && card_issuer != type)
                {
                    foreach (var uri in WithFallback($"avares://Financisto.Common/Assets/ElectronicType/electronic_type_{card_issuer}", iconSet)) yield return uri;
                }

                if (KnownTypes.Contains(type))
                {
                    if (type == "credit_card" || type == "debit_card")
                    {
                        foreach (var uri in WithFallback("avares://Financisto.Common/Assets/AccountType/account_type_card", iconSet)) yield return uri;
                    }
                    else
                    {
                        foreach (var uri in WithFallback($"avares://Financisto.Common/Assets/AccountType/account_type_{type}", iconSet)) yield return uri;
                    }
                }
            }

            foreach (var uri in WithFallback("avares://Financisto.Common/Assets/AccountType/account_type_other", iconSet)) yield return uri;
        }

        // The chosen set's format first; the other one only if the icon is missing from the chosen set.
        private static IEnumerable<Uri> WithFallback(string basePath, IconSetType iconSet)
        {
            string[] extensions = iconSet == IconSetType.Monocolor ? new[] { ".svg", ".png" } : new[] { ".png", ".svg" };
            foreach (string extension in extensions)
            {
                yield return new Uri(basePath + extension);
            }
        }
    }
}
