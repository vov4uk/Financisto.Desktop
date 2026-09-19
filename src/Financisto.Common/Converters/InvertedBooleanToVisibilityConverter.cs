namespace Financisto.Converters
{
    /// <summary>
    /// Binds to a control's IsVisible property (Avalonia has no Visibility enum).
    /// </summary>
    public sealed class InvertedBooleanToVisibilityConverter : BooleanConverter<bool>
    {
        public InvertedBooleanToVisibilityConverter() :
            base(false, true)
        { }
    }
}
