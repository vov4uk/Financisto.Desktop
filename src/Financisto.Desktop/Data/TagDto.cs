using System;
using System.Linq;
using Financisto.DataAccess.Data;
using Financisto.DataAccess.Utils;
using Prism.Mvvm;

namespace Financisto.Desktop.Data
{
    public class TagDto : BindableBase
    {
        private readonly string originalAliases;
        private bool isActive;
        private string title;
        private string aliases;

        public TagDto(TagBase proj)
        {
            this.Title = proj.Title;
            this.IsActive = proj.IsActive;

            if (proj is IHasAliases withAliases)
            {
                SupportsAliases = true;
                originalAliases = BackupText.Unescape(withAliases.Aliases) ?? string.Empty;
                this.Aliases = originalAliases;
            }
        }

        public TagDto(string title, bool isActive)
        {
            this.Title = title;
            this.IsActive = isActive;
        }

        public bool IsActive
        {
            get => isActive;
            set { SetProperty(ref isActive, value, nameof(IsActive)); }
        }

        public string Title
        {
            get => title;
            set { SetProperty(ref title, value, nameof(Title)); }
        }

        /// <summary>Payees and locations have aliases; projects and tags don't.</summary>
        public bool SupportsAliases { get; }

        /// <summary>Aliases for editing, one per line.</summary>
        public string Aliases
        {
            get => aliases;
            set { SetProperty(ref aliases, value, nameof(Aliases)); }
        }

        public void ApplyAliases(TagBase entity)
        {
            if (entity is not IHasAliases withAliases || (Aliases ?? string.Empty) == originalAliases)
            {
                return; // untouched aliases keep their exact source text
            }

            var lines = (Aliases ?? string.Empty)
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            withAliases.Aliases = lines.Length > 0 ? BackupText.Escape(string.Join("\n", lines)) : null;
        }
    }
}
