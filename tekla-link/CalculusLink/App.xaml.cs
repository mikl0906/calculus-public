using System.IO;
using System.Windows;

using Microsoft.Toolkit.Uwp.Notifications;

using Serilog;

namespace CalculusLink;

public partial class App : Application
{
    public App()
    {
        // Initialize local folder at User/AppData/Local/CalculusLink
        var folderService = new FolderService("CalculusLink");

        // Setup Serilog to log to a file in the local folder
        Log.Logger = new LoggerConfiguration()
            .WriteTo.File(Path.Combine(folderService.LogDirectory, "log.txt"))
            .CreateLogger();

        // Handle toast activation
        ToastNotificationManagerCompat.OnActivated += toastArgs =>
        {
            ToastArguments args = ToastArguments.Parse(toastArgs.Argument);

            if (args.TryGetValue("action", out string action) && action == "update")
            {
                _ = Updater.DownloadLatestTsep();
            }
        };

        // Check for updates on startup
        _ = Updater.CheckForUpdates();
    }
}