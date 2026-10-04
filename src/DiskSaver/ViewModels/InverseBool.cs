using System.Globalization;
using System.Windows.Data;

namespace DiskSaver.ViewModels;

public sealed class InverseBool : IValueConverter
{
    public static readonly InverseBool Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
