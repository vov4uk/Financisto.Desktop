using Financisto.Desktop.Data;
using Prism.Commands;

namespace Financisto.Desktop.ViewModels.Dialogs
{
    public class LocationDialogVM : TagDialogVM
    {
        private DelegateCommand _clearAddressCommand;

        public LocationDialogVM(LocationDto location) : base(location)
        {
            Entity = location;
        }

        public DelegateCommand ClearAddressCommand => _clearAddressCommand ??= new DelegateCommand(() => { Entity.Address = default; });

        public new LocationDto Entity { get; }
    }
}
