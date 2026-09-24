using Financisto.Desktop.Data;
using Prism.Commands;

namespace Financisto.Desktop.ViewModels.Dialogs
{
    public class TagDialogVM : DialogBaseVM
    {
        private DelegateCommand _clearTitleCommand;

        public TagDialogVM(TagDto entity)
        {
            this.Entity = entity;
        }

        public DelegateCommand ClearTitleCommand => _clearTitleCommand ??= new DelegateCommand(() => { Entity.Title = default; });

        public TagDto Entity { get; }

        public bool ShowAliases => Entity.SupportsAliases;
        public override object OnRequestSave()
        {
            return Entity;
        }
    }
}
