using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Data;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;

namespace Zeiterfassung.Services;

/// <summary>Lädt die Daten eines Zeitraums und berechnet Tages-, Überstunden- und Urlaubswerte.</summary>
public sealed class BalanceService(
    IDbContextFactory<AppDbContext> factory,
    SettingsService settings,
    TimeProvider time)
{
    public DayResult GetDay(DateOnly date) => GetRange(date, date)[0];

    public IReadOnlyList<DayResult> GetRange(DateOnly from, DateOnly to)
    {
        using var db = factory.CreateDbContext();

        var profiles = db.WorkTimeProfiles
            .Include(p => p.Overrides)
            .OrderBy(p => p.ValidFrom)
            .ToList();

        var entriesByDate = db.TimeEntries
            .Include(e => e.Customer)
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Start)
            .AsEnumerable()
            .GroupBy(e => e.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TimeEntry>)g.ToList());

        var pausesByDate = db.PauseEntries
            .Where(p => p.Date >= from && p.Date <= to)
            .AsEnumerable()
            .GroupBy(p => p.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<PauseEntry>)g.ToList());

        var absencesByDate = db.Absences
            .Where(a => a.Date >= from && a.Date <= to)
            .AsEnumerable()
            .GroupBy(a => a.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Absence>)g.ToList());

        var state = settings.GetFederalState();
        var holidaysByDate = db.Holidays
            .Where(h => h.State == state && h.Date >= from && h.Date <= to)
            .ToDictionary(h => h.Date, h => h.Name);

        var result = new List<DayResult>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var profile = profiles.LastOrDefault(p => p.ValidFrom <= date);
            var entries = entriesByDate.GetValueOrDefault(date, []);
            var absences = absencesByDate.GetValueOrDefault(date, []);
            var pauses = pausesByDate.GetValueOrDefault(date, []);
            holidaysByDate.TryGetValue(date, out var holidayName);

            result.Add(DayCalculator.Calculate(date, profile, holidayName, absences, entries, pauses));
        }

        return result;
    }

    /// <summary>
    /// Überstundensaldo bis einschließlich <paramref name="asOf"/>. Zukünftige Tage zählen nicht.
    /// Der Startsaldo gilt bis einschließlich seines Stichtags.
    /// </summary>
    public int GetOvertimeBalance(DateOnly asOf)
    {
        using var db = factory.CreateDbContext();
        var opening = db.OvertimeOpenings.OrderBy(o => o.Id).FirstOrDefault();
        var earliestProfile = db.WorkTimeProfiles.OrderBy(p => p.ValidFrom).FirstOrDefault();

        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var end = asOf < today ? asOf : today;

        var baseMinutes = opening?.Minutes ?? 0;
        var start = opening is not null
            ? opening.AsOfDate.AddDays(1)
            : earliestProfile?.ValidFrom;

        if (start is null || end < start.Value)
        {
            return baseMinutes;
        }

        return baseMinutes + GetRange(start.Value, end).Sum(d => d.BalanceMinutes);
    }

    /// <summary>Urlaubskonto eines Jahres. Der Übertrag stammt aus dem Vorjahr oder aus dem manuellen Startwert.</summary>
    public VacationStatus GetVacation(int year)
    {
        using var db = factory.CreateDbContext();
        var entitlement = db.VacationEntitlements.FirstOrDefault(v => v.Year == year);

        var carryOver = GetCarryOver(year, entitlement);
        var taken = GetRange(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31))
            .Sum(d => d.VacationDaysUsed);

        return new VacationStatus(
            year,
            entitlement?.Days ?? 0m,
            entitlement?.CorrectionDays ?? 0m,
            carryOver,
            taken);
    }

    private decimal GetCarryOver(int year, VacationEntitlement? entitlement)
    {
        if (entitlement?.OpeningCarryOverDays is { } opening)
        {
            return opening;
        }

        using var db = factory.CreateDbContext();
        var previousYearExists = db.VacationEntitlements.Any(v => v.Year == year - 1);
        return previousYearExists ? GetVacation(year - 1).RemainingDays : 0m;
    }
}
