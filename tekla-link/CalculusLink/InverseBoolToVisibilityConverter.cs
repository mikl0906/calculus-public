using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CalculusLink;

/// <summary>Visible when false: the card's Preview button, hidden while a preview is open.</summary>
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
