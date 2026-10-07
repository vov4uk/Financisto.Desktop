using System.Diagnostics.CodeAnalysis;

namespace Financisto.Common.Model
{
    /// <summary>A row of the SMS templates page: a template shared with the Android app, or a desktop-only import rule.</summary>
    [ExcludeFromCodeCoverage]
    public class SmsTemplateModel : BaseModel, IActive
    {
        public int? Id { get; set; }

        public bool IsActive { get; set; }

        /// <summary>The sender (package / title / SMS number) of a template; the generated title of a rule.</summary>
        public string Title { get; set; }

        public string Description { get; set; }

        public string Template { get; set; }

        public bool IsRule { get; set; }

        public string Kind { get; set; }

        public string Category { get; set; }

        public string Payee { get; set; }

        public string Project { get; set; }

        public string Location { get; set; }

        public string Account { get; set; }
    }
}
