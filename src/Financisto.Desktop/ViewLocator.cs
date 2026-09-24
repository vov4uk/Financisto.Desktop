using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Financisto.Desktop.ViewModels.Pages;
using Financisto.Desktop.Views;
using Prism.Mvvm;

namespace Financisto.Desktop;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    // Page view models live under ViewModels.Pages with short names (e.g. BlotterVM) that
    // don't follow the "XyzViewModel" -> "XyzView" naming convention below, so they're mapped explicitly.
    private static readonly Dictionary<Type, Type> PageViews = new()
    {
        [typeof(AccountsPageVM)] = typeof(AccountsPageView),
        [typeof(CategoriesPageVM)] = typeof(CategoriesPageView),
        [typeof(CurrenciesPageVM)] = typeof(CurrenciesPageView),
        [typeof(ExchangeRatesPageVM)] = typeof(ExchangeRatesPageView),
        [typeof(LocationsPageVM)] = typeof(LocationsPageView),
        [typeof(PayeesPageVM)] = typeof(TagPageView),
        [typeof(TagsPageVM)] = typeof(TagPageView),
        [typeof(ProjectsPageVM)] = typeof(TagPageView),
        [typeof(BlotterPageVM)] = typeof(BlotterPageView),
        [typeof(SettingsPageVM)] = typeof(SettingsPageView),
        [typeof(DashboardPageVM)] = typeof(DashboardPageView),
    };

    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var vmType = param.GetType();
        if (PageViews.TryGetValue(vmType, out var pageViewType))
        {
            return (Control)Activator.CreateInstance(pageViewType)!;
        }

        var name = vmType.FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        return new TextBlock { Text = "Not Found: " + name };
    }


    public bool Match(object? data)
    {
        return data is BindableBase;
    }
}
