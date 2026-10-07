using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Financisto.Desktop.Wizards
{
    /// <summary>Invariant double for optional columns; an empty or invalid cell becomes null instead of failing the row.</summary>
    public class DoubleConvert : DoubleConverter
    {
        public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            bool isNum = double.TryParse(text, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out double retNum);
            if (!isNum)
            {
                return default(double?)!;
            }
            return retNum;
        }
    }
}
