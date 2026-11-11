using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Data;
using Zeiterfassung.Integration;
using Zeiterfassung.Models;

namespace Zeiterfassung.Services;

/// <summary>Feiertage für Jahr und Bundesland. Gespeicherte Daten bleiben offline nutzbar.</summary>
public sealed class HolidayService(
    IDbContextFactory<AppDbContext> factory,
    HolidayApiClient apiClient,
    TimeProvider time)
{
    public IReadOnlyList<Holiday> GetHolidays(int year, string state)
    {
        using var db = factory.CreateDbContext();
        return db.Holidays
            .Where(h => h.State == state && h.Date.Year == year)
            .OrderBy(h => h.Date)
            .ToList();
    }

    /// <summary>Ruft die Feiertage ab und ersetzt den lokalen Stand für Jahr und Bundesland.</summary>
    public async Task<int> RefreshAsync(int year, string state, CancellationToken cancellationToken = default)
    {
        var json = await apiClient.GetPublicHolidaysJsonAsync(year, cancellationToken);
        var fetchedAt = time.GetLocalNow().DateTime;
        var holidays = HolidayParser.Parse(json, state, fetchedAt)
            .Where(h => h.Date.Year == year)
            .ToList();

        using var db = factory.CreateDbContext();
        var existing = db.Holidays.Where(h => h.State == state && h.Date.Year == year).ToList();
        db.Holidays.RemoveRange(existing);
        db.Holidays.AddRange(holidays);
        db.SaveChanges();

        return holidays.Count;
    }
}
