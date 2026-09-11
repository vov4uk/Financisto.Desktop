using Avalonia;
using Avalonia.Media;
using System;

namespace Financisto.Desktop.ViewModels
{
    // Modelo usado no menu lateral
    public class ListItemTemplate
    {
        public ListItemTemplate(Type type, string label, string iconKey)
        {
            ModelType = type;
            Label = label;

            if (Application.Current is not null &&
                Application.Current.TryGetResource(iconKey, null, out var resource) &&
                resource is StreamGeometry geometry)
            {
                ListItemIcon = geometry;
            }
        }

        public string Label { get; }
        public StreamGeometry? ListItemIcon { get; }
        public Type ModelType { get; }
    }
}
