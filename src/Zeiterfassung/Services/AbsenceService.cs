using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Data;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;

namespace Zeiterfassung.Services;

public sealed class AbsenceService(IDbContextFactory<AppDbContext> factory)
{
    public IReadOnlyList<Absence> GetAbsences(DateOnly from, DateOnly to)
    {
        using var db = factory.CreateDbContext();
        return db.Absences
            .Where(a => a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date)
            .ToList();
    }

    public Absence AddAbsence(DateOnly date, AbsenceType type, decimal portion, string? note) =>
        AddRange(date, date, type, portion, note, includeWeekends: true).Single();

    /// <summary>
    /// Legt für jeden Tag von <paramref name="from"/> bis <paramref name="to"/> eine Abwesenheit an.
    /// Ohne <paramref name="includeWeekends"/> werden Samstage und Sonntage übersprungen.
    /// Gibt es schon einen Eintrag dieses Typs an einem der Tage, wird nichts gespeichert.
    /// </summary>
    public IReadOnlyList<Absence> AddRange(
        DateOnly from,
        DateOnly to,
        AbsenceType type,
        decimal portion,
        string? note,
        bool includeWeekends)
    {
        if (portion is not (0.5m or 1m))
        {
            throw new DomainException("Eine Abwesenheit ist ganz (1) oder halb (0,5) Tage.");
        }

        if (to < from)
        {
            throw new DomainException("Das Enddatum liegt vor dem Startdatum.");
        }

        var dates = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(offset => from.AddDays(offset))
            .Where(date => includeWeekends || date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            .ToList();

        if (dates.Count == 0)
        {
            throw new DomainException("Im gewählten Zeitraum liegt kein Werktag. Bitte „Wochenende mitzählen“ aktivieren oder den Zeitraum ändern.");
        }

        using var db = factory.CreateDbContext();
        // Pro Tag ist nur eine Abwesenheit erlaubt. Einzige Ausnahme: halber Urlaub zusammen mit halbem Freizeitausgleich.
        var duplicates = db.Absences
            .Where(a => dates.Contains(a.Date))
            .ToList()
            .Where(a => !Absence.IsHalfDayPair(a.Type, a.Portion, type, portion))
            .Select(a => a.Date)
            .ToList();
        if (duplicates.Count > 0)
        {
            var list = string.Join(", ", duplicates.Order().Distinct().Select(d => d.ToString("dd.MM.yyyy")));
            throw new DomainException($"Für diese Tage ist bereits eine Abwesenheit eingetragen (pro Tag nur eine erlaubt): {list}.");
        }

        // An einem Tag kann nicht gearbeitet und zugleich abwesend sein, außer bei Freizeitausgleich und halbem Urlaub.
        var workedDates = Absence.AllowsWork(type, portion)
            ? new List<DateOnly>()
            : db.TimeEntries.Where(e => dates.Contains(e.Date)).Select(e => e.Date)
                .Union(db.PauseEntries.Where(p => dates.Contains(p.Date)).Select(p => p.Date))
                .ToList();
        if (workedDates.Count > 0)
        {
            var list = string.Join(", ", workedDates.Order().Distinct().Select(d => d.ToString("dd.MM.yyyy")));
            throw new DomainException($"An diesen Tagen sind bereits Buchungen oder Pausen eingetragen, eine Abwesenheit ist dort nicht möglich: {list}.");
        }

        var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var absences = dates.Select(date => new Absence
        {
            Date = date,
            Type = type,
            Portion = portion,
            Note = cleanNote
        }).ToList();

        db.Absences.AddRange(absences);
        db.SaveChanges();
        return absences;
    }

    public void DeleteAbsence(int id)
    {
        using var db = factory.CreateDbContext();
        var absence = db.Absences.Find(id);
        if (absence is not null)
        {
            db.Absences.Remove(absence);
            db.SaveChanges();
        }
    }
}
