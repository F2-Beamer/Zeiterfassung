using System.IO;
using FluentAssertions;
using Zeiterfassung.Services;

namespace Zeiterfassung.Tests;

public class StoragePathResolverTests
{
    private const string ExeDirectory = @"C:\Tools\Zeiterfassung";
    private const string AppDataDirectory = @"C:\Users\test\AppData\Roaming";

    [Fact]
    public void DataDirectory_IstExeOrdner_WennBeschreibbar()
    {
        var resolver = new StoragePathResolver(ExeDirectory, AppDataDirectory, _ => true);

        resolver.DataDirectory.Should().Be(ExeDirectory);
    }

    [Fact]
    public void DataDirectory_WeichtAufAppData_Aus_WennExeOrdnerGesperrt()
    {
        var resolver = new StoragePathResolver(ExeDirectory, AppDataDirectory, _ => false);

        resolver.DataDirectory.Should().Be(Path.Combine(AppDataDirectory, "Zeiterfassung"));
    }

    [Fact]
    public void DatabasePath_LiegtImDatenordner()
    {
        var resolver = new StoragePathResolver(ExeDirectory, AppDataDirectory, _ => true);

        resolver.DatabasePath.Should().Be(Path.Combine(ExeDirectory, "zeiterfassung.db"));
    }

    [Fact]
    public void Pruefung_WirdFuerExeOrdnerAufgerufen()
    {
        var checkedDirectories = new List<string>();
        var resolver = new StoragePathResolver(ExeDirectory, AppDataDirectory, dir =>
        {
            checkedDirectories.Add(dir);
            return true;
        });

        _ = resolver.DataDirectory;

        checkedDirectories.Should().ContainSingle().Which.Should().Be(ExeDirectory);
    }
}
