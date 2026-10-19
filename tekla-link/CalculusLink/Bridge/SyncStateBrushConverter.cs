using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CalculusLink.Bridge;

/// <summary>
/// A preview row's state dot: the same colours <see cref="SyncPreview"/> paints
/// both models with, so the list reads as the legend.
/// </summary>
public class SyncStateBrushConverter : IValueConverter
{
    private static readonly Dictionary<SyncState, Brush> Brushes = new()
    {
        [SyncState.New] = Frozen("#3fb950"),
        [SyncState.Updated] = Frozen("#d8a630"),
        [SyncState.Conflict] = Frozen("#f0883e"),
        [SyncState.Gone] = Frozen("#f85149"),
        [SyncState.Unchanged] = Frozen("#8b949e"),
    };

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SyncState state ? Brushes[state] : Brushes[SyncState.Unchanged];

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
