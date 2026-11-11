using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Data;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;

namespace Zeiterfassung.Services;

/// <summary>Zeitbuchungen und der Live-Timer.</summary>
public sealed class TimeTrackingService(IDbContextFactory<AppDbContext> factory, TimeProvider time)
{
    public IReadOnlyList<TimeEntry> GetEntries(DateOnly date)
    {
        using var db = factory.CreateDbContext();
        return db.TimeEntries
            .Include(e => e.Customer)
            .Where(e => e.Date == date)
            .OrderBy(e => e.Start)
            .ToList();
    }

    public TimeEntry AddEntry(TimeEntry entry)
    {
        using var db = factory.CreateDbContext();
        ValidateOrThrow(db, entry);

        db.TimeEntries.Add(entry);
        db.SaveChanges();
        return entry;
    }

    public void UpdateEntry(TimeEntry entry)
    {
        using var db = factory.CreateDbContext();
        ValidateOrThrow(db, entry);

        var existing = db.TimeEntries.Find(entry.Id)
            ?? throw new DomainException("Die Buchung existiert nicht mehr.");

        existing.CustomerId = entry.CustomerId;
        existing.Date = entry.Date;
        existing.Start = entry.Start;
        existing.End = entry.End;
        existing.BreakMinutes = entry.BreakMinutes;
        existing.Note = entry.Note;
        db.SaveChanges();
    }

    public IReadOnlyList<PauseEntry> GetPauses(DateOnly date)
    {
        using var db = factory.CreateDbContext();
        return db.PauseEntries
            .Where(p => p.Date == date)
            .OrderBy(p => p.Start)
            .ToList();
    }

    /// <summary>Legt eine Pause ohne Kundenbezug an. Pausen am selben Tag dürfen sich nicht überschneiden.</summary>
    public PauseEntry AddPause(DateOnly date, TimeOnly start, TimeOnly end)
    {
        if (end <= start)
        {
            throw new DomainException("Das Pausenende muss nach dem Pausenbeginn liegen.");
        }

        using var db = factory.CreateDbContext();
        ValidateNoAbsence(db, date);

        var overlaps = db.PauseEntries.Any(p => p.Date == date && p.Start < end && start < p.End);
        if (overlaps)
        {
            throw new DomainException("Diese Pause überschneidet sich mit einer anderen Pause des Tages.");
        }

        var pause = new PauseEntry { Date = date, Start = start, End = end };
        db.PauseEntries.Add(pause);
        db.SaveChanges();
        return pause;
    }

    public void DeletePause(int id)
    {
        using var db = factory.CreateDbContext();
        var existing = db.PauseEntries.Find(id);
        if (existing is not null)
        {
            db.PauseEntries.Remove(existing);
            db.SaveChanges();
        }
    }

    public void DeleteEntry(int id)
    {
        using var db = factory.CreateDbContext();
        var existing = db.TimeEntries.Find(id);
        if (existing is not null)
        {
            db.TimeEntries.Remove(existing);
            db.SaveChanges();
        }
    }

    public ActiveTimer? GetActiveTimer()
    {
        using var db = factory.CreateDbContext();
        return db.ActiveTimers.Find(ActiveTimer.SingletonId);
    }

    /// <summary>Startet den Timer für einen Kunden. Ein laufender Timer wird vorher gestoppt und gebucht.</summary>
    public TimeEntry? StartTimer(int customerId)
    {
        using var db = factory.CreateDbContext();

        var customer = db.Customers.Find(customerId)
            ?? throw new DomainException("Der Kunde existiert nicht.");
        if (customer.IsArchived)
        {
            throw new DomainException("Archivierte Kunden können nicht gestartet werden.");
        }

        var stopped = StopTimerInternal(db);

        var now = Truncate(time.GetLocalNow().DateTime);
        db.ActiveTimers.Add(new ActiveTimer
        {
            CustomerId = customerId,
            StartedAt = now
        });
        db.SaveChanges();

        return stopped;
    }

    public void PauseTimer()
    {
        using var db = factory.CreateDbContext();
        var timer = db.ActiveTimers.Find(ActiveTimer.SingletonId);
        if (timer is null || timer.PausedAt is not null)
        {
            return;
        }

        timer.PausedAt = Truncate(time.GetLocalNow().DateTime);
        db.SaveChanges();
    }

    public void ResumeTimer()
    {
        using var db = factory.CreateDbContext();
        var timer = db.ActiveTimers.Find(ActiveTimer.SingletonId);
        if (timer?.PausedAt is not { } pausedAt)
        {
            return;
        }

        var now = Truncate(time.GetLocalNow().DateTime);
        timer.AccumulatedBreakSeconds += (int)(now - pausedAt).TotalSeconds;
        timer.PausedAt = null;
        db.SaveChanges();
    }

    /// <summary>Stoppt den Timer und speichert die Buchung. Liefert null, wenn kein Timer läuft.</summary>
    public TimeEntry? StopTimer()
    {
        using var db = factory.CreateDbContext();
        var entry = StopTimerInternal(db);
        db.SaveChanges();
        return entry;
    }

    private TimeEntry? StopTimerInternal(AppDbContext db)
    {
        var timer = db.ActiveTimers.Find(ActiveTimer.SingletonId);
        if (timer is null)
        {
            return null;
        }

        var now = Truncate(time.GetLocalNow().DateTime);
        var breakSeconds = timer.AccumulatedBreakSeconds;
        if (timer.PausedAt is { } pausedAt)
        {
            breakSeconds += (int)(now - pausedAt).TotalSeconds;
        }

        if (DateOnly.FromDateTime(timer.StartedAt) != DateOnly.FromDateTime(now))
        {
            throw new DomainException(
                "Der Timer läuft über Mitternacht. Bitte die Buchung manuell aufteilen und den Timer verwerfen.");
        }

        if (now - timer.StartedAt < TimeSpan.FromMinutes(1))
        {
            // Unter einer Minute wird nichts gebucht. Der Timer wird verworfen, damit der Wechsel zu einem anderen Kunden klappt.
            db.ActiveTimers.Remove(timer);
            return null;
        }

        var entry = new TimeEntry
        {
            CustomerId = timer.CustomerId,
            Date = DateOnly.FromDateTime(timer.StartedAt),
            Start = TimeOnly.FromDateTime(timer.StartedAt),
            End = TimeOnly.FromDateTime(now),
            BreakMinutes = (int)Math.Round(breakSeconds / 60.0, MidpointRounding.AwayFromZero),
            FromTimer = true
        };

        ValidateOrThrow(db, entry);

        db.TimeEntries.Add(entry);
        db.ActiveTimers.Remove(timer);
        return entry;
    }

    private static void ValidateOrThrow(AppDbContext db, TimeEntry entry)
    {
        ValidateNoAbsence(db, entry.Date);

        var sameDay = db.TimeEntries.Where(e => e.Date == entry.Date).ToList();
        var errors = TimeEntryValidator.Validate(entry, sameDay);
        if (errors.Count > 0)
        {
            throw new DomainException(string.Join("\n", errors));
        }
    }

    /// <summary>
    /// An einem Tag mit Abwesenheit kann nicht gearbeitet oder pausiert werden.
    /// Ausnahmen sind Freizeitausgleich und halber Urlaub (siehe <see cref="Absence.AllowsWork"/>).
    /// </summary>
    private static void ValidateNoAbsence(AppDbContext db, DateOnly date)
    {
        var blocking = db.Absences.Where(a => a.Date == date).ToList()
            .Any(a => !Absence.AllowsWork(a.Type, a.Portion));
        if (blocking)
        {
            throw new DomainException(
                $"Am {date:dd.MM.yyyy} ist eine Abwesenheit eingetragen. An diesem Tag kann nicht gearbeitet werden.");
        }
    }

    private static DateTime Truncate(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerMinute);
}
