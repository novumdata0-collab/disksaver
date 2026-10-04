using System.Globalization;
using System.Windows.Data;

namespace DiskSaver.ViewModels;

public sealed class NotNull : IValueConverter
{
    public static readonly NotNull Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
