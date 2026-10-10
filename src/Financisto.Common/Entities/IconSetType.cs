using Financisto.Common.Attribute;

namespace Financisto.Common.Entities
{
    /// <summary>Which artwork the account type/issuer icons are drawn from.</summary>
    public enum IconSetType
    {
        /// <summary>Coloured raster icons (png). First member so settings saved before this option existed load as Default.</summary>
        [LocalizedDescription("icon_set_default")]
        Default,

        /// <summary>Single-colour vector icons (svg).</summary>
        [LocalizedDescription("icon_set_monocolor")]
        Monocolor,
    }
}
