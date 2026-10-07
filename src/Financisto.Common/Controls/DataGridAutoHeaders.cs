using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Financisto.Common.Localization;

namespace Financisto.Common.Controls
{
    /// <summary>
    /// Read-only grid that builds its columns from the row type: only properties with a
    /// <see cref="DisplayNameAttribute"/> get a column, and the attribute's value is a localization key for the header.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class DataGridAutoHeaders : DataGrid
    {
        public DataGridAutoHeaders()
        {
            AutoGenerateColumns = true;
            IsReadOnly = true;
            AutoGeneratingColumn += DataGridAutoHeadersAutoGeneratingColumn;
        }

        // A DataGrid subclass has no control theme of its own; without this it would be drawn unstyled.
        protected override Type StyleKeyOverride => typeof(DataGrid);

        private void DataGridAutoHeadersAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            string propertyDisplayName = GetPropertyDisplayName(GetItemType(), e.PropertyName);

            if (string.IsNullOrEmpty(propertyDisplayName))
            {
                e.Cancel = true;
                return;
            }

            e.Column.Header = LocalizationService.Instance[propertyDisplayName];

            var propertyType = Nullable.GetUnderlyingType(e.PropertyType) ?? e.PropertyType;
            if (propertyType == typeof(long) || propertyType == typeof(double))
            {
                e.Column.CellStyleClasses.Add("numeric");
            }
        }

        private Type GetItemType()
        {
            var sourceType = ItemsSource?.GetType();
            var enumerable = sourceType?.GetInterfaces()
                .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>));
            return enumerable?.GetGenericArguments()[0];
        }

        private static string GetPropertyDisplayName(Type itemType, string propertyName)
        {
            var attribute = itemType?.GetProperty(propertyName)?.GetCustomAttribute<DisplayNameAttribute>();
            return attribute == null || attribute == DisplayNameAttribute.Default ? null : attribute.DisplayName;
        }
    }
}
