using Zeiterfassung.Models;

namespace Zeiterfassung.Domain;

/// <summary>Prüft eine Zeitbuchung gegen ihre Regeln. Liefert eine Liste von Fehlermeldungen (leer = gültig).</summary>
public static class TimeEntryValidator
{
    public static IReadOnlyList<string> Validate(TimeEntry candidate, IEnumerable<TimeEntry> sameDayEntries)
    {
        var errors = new List<string>();

        if (candidate.End <= candidate.Start)
        {
            // TimeOnly kann nicht über Mitternacht gehen. Ende vor Start heißt: Buchung ist ungültig.
            errors.Add("Die Endzeit muss nach der Startzeit liegen. Buchungen über Mitternacht bitte aufteilen.");
            return errors;
        }

        var duration = (int)(candidate.End - candidate.Start).TotalMinutes;
        if (candidate.BreakMinutes < 0)
        {
            errors.Add("Die Pausenzeit darf nicht negativ sein.");
        }
        else if (candidate.BreakMinutes >= duration)
        {
            errors.Add("Die Pausenzeit muss kürzer als die Gesamtdauer sein.");
        }

        var overlaps = sameDayEntries.Any(other =>
            other.Id != candidate.Id &&
            candidate.Start < other.End &&
            other.Start < candidate.End);

        if (overlaps)
        {
            errors.Add("Die Buchung überschneidet sich mit einer bestehenden Buchung.");
        }

        return errors;
    }
}
