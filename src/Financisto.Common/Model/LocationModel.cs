using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Financisto.DataAccess.Utils;

namespace Financisto.Common.Model
{
    [ExcludeFromCodeCoverage]
    public class LocationModel : TagBaseModel
    {
        [Column("resolved_address")]
        public string Address { get; set; }

        [Column("aliases")]
        public string Aliases { get; set; }

        public override string AliasesText => string.Join(", ", BackupText.SplitAliases(Aliases));
    }
}
