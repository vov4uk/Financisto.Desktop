using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Financisto.BankHelpers
{
    /// <summary>
    /// Optional base class for <see cref="IBankHelper"/>. It reads <see cref="Icon"/> from the embedded
    /// resource named <c>icon.png</c> of the plugin assembly (the plugin build embeds <c>icon.png</c> from the project folder).
    /// </summary>
    public abstract class BankHelperBase : IBankHelper
    {
        public const string IconResourceName = "icon.png";

        private readonly Lazy<byte[]?> icon;

        protected BankHelperBase()
        {
            icon = new Lazy<byte[]?>(LoadIcon);
        }

        public abstract string BankTitle { get; }

        public abstract ReportType ReportType { get; }

        public virtual byte[]? Icon => icon.Value;

        public abstract IEnumerable<BankTransaction> ParseReport(string filePath);

        /// <summary>Picks the text for the UI language, e.g. <c>Localized("Privat", ("uk", "Приват"))</c>.</summary>
        public static string Localized(string defaultText, params (string Language, string Text)[] translations)
        {
            var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            foreach (var (translationLanguage, text) in translations)
            {
                if (string.Equals(translationLanguage, language, StringComparison.OrdinalIgnoreCase))
                {
                    return text;
                }
            }

            return defaultText;
        }

        private byte[]? LoadIcon()
        {
            using var stream = GetType().Assembly.GetManifestResourceStream(IconResourceName);
            if (stream is null)
            {
                return null;
            }

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
    }
}
