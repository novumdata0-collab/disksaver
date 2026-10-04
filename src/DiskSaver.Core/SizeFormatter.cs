using System.Globalization;

namespace DiskSaver.Core;

public static class SizeFormatter
{
    public static string Format(long bytes)
    {
        var units = CoreText.Get("SizeUnits").Split(',');
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        var format = unit == 0 || value >= 100 ? "0" : "0.#";
        return value.ToString(format, CultureInfo.CurrentCulture) + " " + units[unit];
    }
}
