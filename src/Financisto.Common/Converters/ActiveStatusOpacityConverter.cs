using System.Diagnostics.CodeAnalysis;

namespace Financisto.Converters
{
    /// <summary>
    /// Used to visually dim DataGridRow items whose IsActive is false, without relying on
    /// Classes.* binding setters (which Avalonia disallows on styles using an activator selector).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed class ActiveStatusOpacityConverter : BooleanConverter<double>
    {
        public ActiveStatusOpacityConverter() :
            base(1.0, 0.5)
        { }
    }
}
