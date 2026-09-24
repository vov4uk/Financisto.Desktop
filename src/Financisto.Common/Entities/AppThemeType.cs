using Financisto.Common.Attribute;

namespace Financisto.Common.Entities
{
    public enum AppThemeType
    {
        [LocalizedDescription("app_theme_system")]
        System,
        [LocalizedDescription("app_theme_light")]
        Light,
        [LocalizedDescription("app_theme_dark")]
        Dark
    }
}