using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;

namespace Financisto.Common.Utils
{
    /// <summary>
    /// Port of Android's <c>Color.parseColor</c>, which the Android app uses for <c>account.accent_color</c>:
    /// "#RRGGBB", "#AARRGGBB" or one of its color names. Avalonia's <c>Color.TryParse</c> accepts more (#RGB, CSS
    /// names) and some names differ (Android "green" is #00FF00, "gray" is #888888), so it isn't used here.
    /// </summary>
    public static class AndroidColor
    {
        private static readonly Dictionary<string, uint> ColorNames = new()
        {
            ["black"] = 0xFF000000,
            ["darkgray"] = 0xFF444444,
            ["gray"] = 0xFF888888,
            ["lightgray"] = 0xFFCCCCCC,
            ["white"] = 0xFFFFFFFF,
            ["red"] = 0xFFFF0000,
            ["green"] = 0xFF00FF00,
            ["blue"] = 0xFF0000FF,
            ["yellow"] = 0xFFFFFF00,
            ["cyan"] = 0xFF00FFFF,
            ["magenta"] = 0xFFFF00FF,
            ["aqua"] = 0xFF00FFFF,
            ["fuchsia"] = 0xFFFF00FF,
            ["darkgrey"] = 0xFF444444,
            ["grey"] = 0xFF888888,
            ["lightgrey"] = 0xFFCCCCCC,
            ["lime"] = 0xFF00FF00,
            ["maroon"] = 0xFF800000,
            ["navy"] = 0xFF000080,
            ["olive"] = 0xFF808000,
            ["purple"] = 0xFF800080,
            ["silver"] = 0xFFC0C0C0,
            ["teal"] = 0xFF008080,
        };

        public static bool TryParse(string text, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(text))
                return false;

            if (text[0] == '#')
            {
                if (text.Length is not (7 or 9) ||
                    !uint.TryParse(text.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var argb))
                    return false;

                color = Color.FromUInt32(text.Length == 7 ? argb | 0xFF000000 : argb);
                return true;
            }

            if (!ColorNames.TryGetValue(text.ToLowerInvariant(), out var named))
                return false;

            color = Color.FromUInt32(named);
            return true;
        }

        /// <summary>Formats a color the way Android's palette writes it: "#rrggbb", or "#aarrggbb" when not opaque.</summary>
        public static string ToHex(Color color) =>
            color.A == 0xFF
                ? $"#{color.R:x2}{color.G:x2}{color.B:x2}"
                : $"#{color.A:x2}{color.R:x2}{color.G:x2}{color.B:x2}";
    }
}
