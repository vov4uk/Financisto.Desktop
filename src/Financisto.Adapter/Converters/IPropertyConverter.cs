using System;

namespace Financisto.Adapter.Converters
{
    public interface IPropertyConverter
    {
        Type PropertyType { get; set; }
        object Convert(object value);

        string ConvertBack(object value);
    }
}
