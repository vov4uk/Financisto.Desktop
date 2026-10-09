using System.ComponentModel;
using Financisto.Common.Entities;

namespace Financisto.Common.Utils
{
    /// <summary>
    /// The icon set currently in use. Bindings that show account icons include <see cref="IconSet"/> as an extra
    /// value of <c>AccountTypeConverter</c>, so changing it re-renders every visible icon.
    /// </summary>
    public sealed class IconSettings : INotifyPropertyChanged
    {
        private IconSetType iconSet;

        public static IconSettings Instance { get; } = new IconSettings();

        private IconSettings()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public IconSetType IconSet
        {
            get => iconSet;
            set
            {
                if (iconSet != value)
                {
                    iconSet = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IconSet)));
                }
            }
        }
    }
}
