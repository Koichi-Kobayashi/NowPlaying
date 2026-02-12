using System.Globalization;
using System.Windows.Data;

namespace NowPlaying.Helpers;

/// <summary>
/// bool の値を反転して変換します。
/// </summary>
internal class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}
