using System.IO;

namespace CalculusLink;

public class FolderService(string localPath)
{
    readonly string _appLocalDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        localPath);

    public string AppLocalDirectory
    {
        get
        {
            if (!Directory.Exists(_appLocalDirectory))
            {
                Directory.CreateDirectory(_appLocalDirectory);
            }
            return _appLocalDirectory;
        }
    }

    public string LogDirectory
    {
        get
        {
            var logDirectory = Path.Combine(AppLocalDirectory, "Logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }
            return logDirectory;
        }
    }
}
