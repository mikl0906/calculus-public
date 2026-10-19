using System.Windows;
using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace CalculusLink;

/// <summary>
/// The Tekla model shown in the title bar (template in MainWindow.xaml): its
/// name and a dot in the success or critical colour. Was the
/// TeklaConnectionStatus package.
/// </summary>
public partial class TeklaConnectionStatus : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionColor))]
    [NotifyPropertyChangedFor(nameof(ToolTip))]
    public partial bool IsConnected { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTip))]
    public partial string? ModelName { get; set; }

    public Brush ConnectionColor => IsConnected
        ? Application.Current.Resources["SystemFillColorSuccessBrush"] as Brush ?? Brushes.Green
        : Application.Current.Resources["SystemFillColorCriticalBrush"] as Brush ?? Brushes.Red;

    public string ToolTip => IsConnected ? $"Connected to {ModelName}" : "Not connected to any Tekla model";

    public void SetConnected(string modelName)
    {
        IsConnected = true;
        ModelName = modelName.Replace(".db1", "");
    }
}
