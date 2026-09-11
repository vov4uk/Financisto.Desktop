using System.Diagnostics.CodeAnalysis;
using Avalonia.Media;

namespace Financisto.Converters
{
    /// <summary>
    /// Replaces the WPF DataTrigger (IsActive=False -> DarkRed) used by the IActive templates;
    /// Avalonia data templates have no DataTemplate.Triggers, so this binds Foreground/Fill directly.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class ActiveStatusBrushConverter : BooleanConverter<IBrush>
    {
        public ActiveStatusBrushConverter() :
            base(Brushes.DarkGreen, Brushes.DarkRed)
        { }
    }
}
