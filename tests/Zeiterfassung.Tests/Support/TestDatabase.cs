using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zeiterfassung.Data;

namespace Zeiterfassung.Tests.Support;

/// <summary>Frische In-Memory-SQLite-Datenbank pro Test. Die Verbindung bleibt offen, damit der Speicher bestehen bleibt.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Factory = new TestContextFactory(options);

        using var db = Factory.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public TestContextFactory Factory { get; }

    public void Dispose() => _connection.Dispose();
}

public sealed class TestContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
}

/// <summary>Uhr, deren Zeit der Test setzt.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;

    public void Advance(TimeSpan span) => Now = Now.Add(span);
}
