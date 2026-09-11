using System.ComponentModel;
using Financisto.Common.Attribute;
using Financisto.Converters;

namespace Financisto.Common.Localization;

[TypeConverter(typeof(EnumDescriptionTypeConverter))]
public enum Language
{
    [LocalizedDescription("language_english")]
    English,
    [LocalizedDescription("language_ukrainian")]
    Ukrainian,
    [LocalizedDescription("language_polish")]
    Polish,
}
