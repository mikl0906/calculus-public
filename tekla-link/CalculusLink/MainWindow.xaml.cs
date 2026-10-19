using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using TSM = Tekla.Structures.Model;

using Wpf.Ui.Controls;
using Wpf.Ui;

namespace CalculusLink;

public partial class MainWindow : FluentWindow
{
    public TeklaConnectionStatus ConnectionStatus { get; } = new();
    public MainWindowViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        Topmost = true;

        ISnackbarService snackbar = new SnackbarService();
        snackbar.SetSnackbarPresenter(SnackbarPresenter);
        ViewModel = new MainWindowViewModel(snackbar);
        // Direct transfer is the tab the window opens on; coming back from
        // Calculus (a model opened or closed meanwhile) refreshes it.
        ViewModel.RefreshCommand.Execute(null);
        Activated += (_, _) =>
        {
            if (ViewModel.ShowDirectTransfer) ViewModel.RefreshCommand.Execute(null);
        };

        var tekalModel = new TSM.Model();
        if (tekalModel.GetConnectionStatus())
        {
            var modeName = tekalModel.GetInfo().ModelName;
            ConnectionStatus.SetConnected(modeName);
        }
    }

    // Enable dragging the window by dragging on any part of the window
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void TopMostButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        TopMostButton.Foreground = Topmost ? (Brush)App.Current.Resources["AccentTextFillColorPrimaryBrush"] : (Brush)App.Current.Resources["TextFillColorPrimaryBrush"];
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsImporting || ViewModel.IsExporting) return;
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Calculus Files (*.calculus)|*.calculus|All Files (*.*)|*.*",
            Multiselect = true
        };
        if (openFileDialog.ShowDialog() == true)
        {
            ViewModel.ImportCommand.Execute(openFileDialog.FileNames);
        }
    }

    private void ImportGrid_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel.IsImporting || ViewModel.IsExporting) return;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            ViewModel.ImportCommand.Execute(files);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsImporting || ViewModel.IsExporting) return;
        ViewModel.ExportCommand.Execute(null);
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CheckUpdateCommand.Execute(null);
    }
}