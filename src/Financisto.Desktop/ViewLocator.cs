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
        [typeof(AccountsVM)] = typeof(AccountsPageView),
        [typeof(CategoriesVM)] = typeof(CategoriesPageView),
        [typeof(CurrenciesVM)] = typeof(CurrenciesPageView),
        [typeof(ExchangeRatesVM)] = typeof(ExchangeRatesPageView),
        [typeof(LocationsVM)] = typeof(LocationsPageView),
        [typeof(PayeesVM)] = typeof(PayeesPageView),
        [typeof(ProjectsVM)] = typeof(ProjectsPageView),
        [typeof(BlotterVM)] = typeof(TransactionsPageView),
        [typeof(SettingsVM)] = typeof(ConfigurationsPageView),
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
