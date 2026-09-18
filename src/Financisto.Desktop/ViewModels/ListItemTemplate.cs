using Avalonia;
using Avalonia.Media;
using Financisto.Common.Localization;
using System;
using System.ComponentModel;

namespace Financisto.Desktop.ViewModels
{
    public class ListItemTemplate : INotifyPropertyChanged
    {
        private readonly Func<string> labelResolver;
        private string label;

        public ListItemTemplate(Type type, Func<string> labelResolver, string iconKey)
        {
            ModelType = type;
            this.labelResolver = labelResolver;
            label = labelResolver();

            if (Application.Current is not null &&
                Application.Current.TryGetResource(iconKey, null, out var resource) &&
                resource is DrawingImage drawingImage)
            {
                ListItemIcon = drawingImage;
            }

            // The sidebar items are created once and live for the app's lifetime,
            // so re-resolving the label on every culture change keeps them in sync
            // without needing to rebuild the collection.
            LocalizationService.Instance.PropertyChanged += OnLocalizationCultureChanged;
        }

        private void OnLocalizationCultureChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LocalizationService.CurrentCulture))
            {
                Label = labelResolver();
            }
        }

        public string Label
        {
            get => label;
            private set
            {
                if (label != value)
                {
                    label = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
                }
            }
        }

        public DrawingImage? ListItemIcon { get; }
        public Type ModelType { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
