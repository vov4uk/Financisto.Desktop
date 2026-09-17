using System.Diagnostics.CodeAnalysis;
using Avalonia.Media;

namespace Financisto.Converters
{
    /// <summary>
    /// Replaces the WPF DataTrigger (HasNoCategory=True -> Pink) used by the transaction title cell;
    /// Avalonia data templates have no DataTemplate.Triggers, so this binds Background directly.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class NoCategoryBackgroundConverter : BooleanConverter<IBrush>
    {
        public NoCategoryBackgroundConverter() :
            base(Brushes.Pink, Brushes.Transparent)
        { }
    }
}
