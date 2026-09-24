using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using Financisto.DataAccess.Utils;

namespace Financisto.Common.Model
{
    [ExcludeFromCodeCoverage]
    public class PayeeModel : TagBaseModel
    {
        [Column("aliases")]
        public string Aliases { get; set; }

        public string AliasesText => string.Join(", ", BackupText.SplitAliases(Aliases));
    }
}
