using System.Globalization;

namespace DiskSaver.Core;

public static class SizeFormatter
{
    private static readonly string[] Units = ["Б", "КБ", "МБ", "ГБ", "ТБ"];

    public static string Format(long bytes)
    {
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var format = unit == 0 || value >= 100 ? "0" : "0.#";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + Units[unit];
    }
}
