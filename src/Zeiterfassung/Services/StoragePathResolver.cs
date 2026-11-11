using System.IO;

namespace Zeiterfassung.Services;

/// <summary>
/// Ermittelt den Datenordner. Standard ist der Ordner neben der EXE.
/// Ist dieser nicht beschreibbar, wird auf %AppData%\Zeiterfassung ausgewichen.
/// </summary>
public sealed class StoragePathResolver
{
    public const string DatabaseFileName = "zeiterfassung.db";
    public const string FolderName = "Zeiterfassung";

    private readonly string _exeDirectory;
    private readonly string _appDataDirectory;
    private readonly Func<string, bool> _isWritable;

    public StoragePathResolver(string exeDirectory, string appDataDirectory, Func<string, bool> isWritable)
    {
        _exeDirectory = exeDirectory;
        _appDataDirectory = appDataDirectory;
        _isWritable = isWritable;
    }

    /// <summary>Liefert den Ordner für Datenbank und Logs.</summary>
    public string DataDirectory
    {
        get
        {
            if (_isWritable(_exeDirectory))
            {
                return _exeDirectory;
            }

            return Path.Combine(_appDataDirectory, FolderName);
        }
    }

    public string DatabasePath => Path.Combine(DataDirectory, DatabaseFileName);

    /// <summary>Produktive Instanz mit echtem Dateisystem.</summary>
    public static StoragePathResolver CreateDefault()
    {
        var exeDirectory = Path.GetDirectoryName(Environment.ProcessPath!)!;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return new StoragePathResolver(exeDirectory, appData, IsWritable);
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }
}
