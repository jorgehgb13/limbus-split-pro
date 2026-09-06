using System.Globalization;
using System.Windows.Data;

namespace LimbusSplitPro.App.Converters;

public sealed class SecondsToTimeStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var seconds = value is double d ? d : 0;
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.ToString(@"mm\:ss");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
