using Avalonia;
using Avalonia.Media;
using System;

namespace Financisto.Desktop.ViewModels
{
    public class ListItemTemplate
    {
        public ListItemTemplate(Type type, string label, string iconKey)
        {
            ModelType = type;
            Label = label;

            if (Application.Current is not null &&
                Application.Current.TryGetResource(iconKey, null, out var resource) &&
                resource is DrawingImage drawingImage)
            {
                ListItemIcon = drawingImage;
            }
        }

        public string Label { get; }
        public DrawingImage? ListItemIcon { get; }
        public Type ModelType { get; }
    }
}
