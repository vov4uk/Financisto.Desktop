using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Financisto.Common.Model
{
    public interface IActive
    {
        public int? Id { get; set; }

        bool IsActive { get; }

        string Title { get; set; }
    }
}
