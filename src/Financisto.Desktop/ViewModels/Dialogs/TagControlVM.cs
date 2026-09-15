using Financisto.Desktop.Data;
using Prism.Commands;

namespace Financisto.Desktop.ViewModels.Dialogs
{
    public class TagControlVM : DialogBaseVM
    {
        private DelegateCommand _clearTitleCommand;

        public TagControlVM(TagDto entity)
        {
            this.Entity = entity;
        }

        public DelegateCommand ClearTitleCommand => _clearTitleCommand ??= new DelegateCommand(() => { Entity.Title = default; });

        public TagDto Entity { get; }
        public override object OnRequestSave()
        {
            return Entity;
        }
    }
}
