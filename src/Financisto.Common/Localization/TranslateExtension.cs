using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace Financisto.Common.Localization;

/// <summary>
/// Avalonia MarkupExtension that binds a UI property to a localized string via
/// <see cref="LocalizationService"/>.
/// </summary>
/// <example>
/// <!-- Declare the namespace in a UserControl/Window/App.axaml -->
/// xmlns:loc="clr-namespace:Financisto.Common.Localization;assembly=Financisto.Common"
///
/// <![CDATA[
/// <Button Content="{loc:Translate Key=Save}" />
/// <TextBlock Text="{loc:Translate Key=AppTitle}" />
/// ]]>
/// </example>
public class TranslateExtension : MarkupExtension
{
    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        // Returning a Binding lets Avalonia's XAML compiler wire it up like a normal {Binding}.
        return new Binding($"[{Key}]") { Source = LocalizationService.Instance };
    }
}
