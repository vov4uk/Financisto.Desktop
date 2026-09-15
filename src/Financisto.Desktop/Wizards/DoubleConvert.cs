using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Financisto.Desktop.Wizards
{
    public class DoubleConvert : DoubleConverter
    {
#nullable enable
        public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            bool isNum = double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.NumberFormatInfo.InvariantInfo, out double retNum);
            if (!isNum)
            {
                return default(double?)!;
            }
            return retNum;
        }
#nullable disable
    }
}
