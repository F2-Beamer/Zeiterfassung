using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Data;
using Zeiterfassung.Domain;

namespace Zeiterfassung.Services;

/// <summary>Erstellt, begrenzt und stellt Datenbank-Sicherungen wieder her.</summary>
public sealed class BackupService(
    StoragePathResolver paths,
    SettingsService settings,
    IDbContextFactory<AppDbContext> factory)
{
    public const int DefaultKeepCount = 10;
    private const string FilePrefix = "zeiterfassung_";

    public string BackupDirectory => Path.Combine(paths.DataDirectory, "backups");

    public string CreateBackup(string? label = null)
    {
        Directory.CreateDirectory(BackupDirectory);

        var suffix = label is null ? string.Empty : $"_{label}";
        var fileName = $"{FilePrefix}{DateTime.Now:yyyyMMdd_HHmmss}{suffix}.db";
        var target = Path.Combine(BackupDirectory, fileName);

        using var source = new SqliteConnection($"Data Source={paths.DatabasePath}");
        source.Open();
        using var destination = new SqliteConnection($"Data Source={target}");
        destination.Open();
        source.BackupDatabase(destination);

        return target;
    }

    /// <summary>Löscht die ältesten Sicherungen, sodass höchstens <paramref name="keep"/> übrig bleiben.</summary>
    public void Prune(int keep = DefaultKeepCount)
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return;
        }

        var obsolete = Directory.EnumerateFiles(BackupDirectory, $"{FilePrefix}*.db")
            .OrderByDescending(Path.GetFileName)
            .Skip(keep);

        foreach (var file in obsolete)
        {
            File.Delete(file);
        }
    }

    /// <summary>Erstellt beim Start eine Sicherung, falls aktiviert und eine Datenbank vorhanden ist.</summary>
    public void AutoBackupOnStart()
    {
        if (!settings.IsAutoBackupEnabled() || !File.Exists(paths.DatabasePath))
        {
            return;
        }

        CreateBackup();
        Prune();
    }

    public IReadOnlyList<string> ListBackups()
    {
        if (!Directory.Exists(BackupDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(BackupDirectory, $"{FilePrefix}*.db")
            .OrderByDescending(Path.GetFileName)
            .ToList();
    }

    /// <summary>
    /// Stellt eine Sicherung wieder her. Der aktuelle Stand wird vorher gesichert.
    /// Die App muss danach neu gestartet werden.
    /// </summary>
    public void Restore(string sourcePath)
    {
        ValidateBackup(sourcePath);

        CreateBackup("vor-wiederherstellung");

        SqliteConnection.ClearAllPools();

        // WAL-Dateien gehören zum alten Stand und dürfen nicht auf die wiederhergestellte Datei angewendet werden.
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            File.Delete(paths.DatabasePath + suffix);
        }

        File.Copy(sourcePath, paths.DatabasePath, overwrite: true);
    }

    private void ValidateBackup(string path)
    {
        if (!File.Exists(path))
        {
            throw new DomainException("Die Sicherungsdatei wurde nicht gefunden.");
        }

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        };

        try
        {
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();

            if (ScalarString(connection, "PRAGMA integrity_check;") != "ok")
            {
                throw new DomainException("Die Sicherungsdatei ist beschädigt.");
            }

            if (ScalarString(connection,
                    "SELECT name FROM sqlite_master WHERE type='table' AND name='Customers';") is null)
            {
                throw new DomainException("Die Datei ist keine Zeiterfassungs-Sicherung.");
            }

            var knownMigrations = factory.CreateDbContext().Database.GetMigrations().ToHashSet();
            var appliedMigrations = ReadAppliedMigrations(connection);
            if (appliedMigrations.Any(m => !knownMigrations.Contains(m)))
            {
                throw new DomainException(
                    "Die Sicherung stammt aus einer neueren Programmversion und kann nicht eingespielt werden.");
            }
        }
        catch (SqliteException ex)
        {
            throw new DomainException("Die Sicherungsdatei konnte nicht gelesen werden: " + ex.Message);
        }
    }

    private static string? ScalarString(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar()?.ToString();
    }

    private static List<string> ReadAppliedMigrations(SqliteConnection connection)
    {
        var migrations = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            migrations.Add(reader.GetString(0));
        }

        return migrations;
    }
}
