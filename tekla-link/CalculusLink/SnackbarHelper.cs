using Wpf.Ui;

namespace CalculusLink;

public class SnackbarHelper(ISnackbarService snackbar)
{
    public void ShowSuccessMessage(string message)
    {
        ShowMessage("Success", message, Wpf.Ui.Controls.ControlAppearance.Success, TimeSpan.FromSeconds(10));
    }

    public void ShowWarningMessage(string message)
    {
        ShowMessage("Warning", message, Wpf.Ui.Controls.ControlAppearance.Caution, TimeSpan.FromSeconds(10));
    }

    public void ShowErrorMessage(string message)
    {
        ShowMessage("Error", message, Wpf.Ui.Controls.ControlAppearance.Danger, TimeSpan.FromSeconds(10));
    }

    public void ShowMessage(string title, string message, Wpf.Ui.Controls.ControlAppearance appearance, TimeSpan timeout)
    {
        App.Current.Dispatcher.Invoke(() => snackbar.Show(title, message, appearance, null, timeout));
    }
}
