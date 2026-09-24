using System;

namespace Financisto.DataAccess.Utils
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class IgnoreAttribute : Attribute
    {
    }
}
