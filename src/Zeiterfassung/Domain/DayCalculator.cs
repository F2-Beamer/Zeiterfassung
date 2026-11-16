using Zeiterfassung.Models;

namespace Zeiterfassung.Domain;

/// <summary>
/// Berechnet die Soll-Zeit eines Tages.
/// Feiertage setzen das Soll auf 0. Urlaub und Krank reduzieren das Soll um ihren Anteil.
/// Freizeitausgleich reduziert das Soll nicht. Die Abbuchung ergibt sich, weil dann keine Ist-Zeit anfällt.
/// </summary>
public static class DayCalculator
{
    public static DayResult Calculate(
        DateOnly date,
        WorkTimeProfile? profile,
        string? holidayName,
        IReadOnlyList<Absence> absences,
        IReadOnlyList<TimeEntry> entries,
        IReadOnlyList<PauseEntry>? pauses = null)
    {
        var baseTarget = TargetCalculator.BaseTargetMinutes(profile, date.DayOfWeek);
        // Pausen ohne Kundenbezug mindern die Ist-Zeit nur, soweit sie in eine Buchung fallen.
        // Liegt eine Pause in einer Lücke zwischen zwei Buchungen, ist sie dort schon nicht gezählt.
        var pauseMinutes = pauses is null ? 0 : OverlapWithEntries(pauses, entries);
        var actual = Math.Max(0, entries.Sum(e => e.NetMinutes) - pauseMinutes);

        var isHoliday = holidayName is not null;
        var reduction = absences
            .Where(a => a.Type is AbsenceType.Vacation or AbsenceType.Sick)
            .Sum(a => a.Portion);
        reduction = Math.Min(1m, reduction);

        var target = isHoliday
            ? 0
            : (int)Math.Round(baseTarget * (1m - reduction), MidpointRounding.AwayFromZero);

        var vacationDays = !isHoliday && baseTarget > 0
            ? absences.Where(a => a.Type == AbsenceType.Vacation).Sum(a => a.Portion)
            : 0m;

        return new DayResult(date, entries, absences, holidayName, actual, target, vacationDays);
    }

    /// <summary>Summe der Minuten, in denen eine Pause in eine Buchung fällt.</summary>
    private static int OverlapWithEntries(IReadOnlyList<PauseEntry> pauses, IReadOnlyList<TimeEntry> entries)
    {
        var total = 0;
        foreach (var pause in pauses)
        {
            foreach (var entry in entries)
            {
                var start = pause.Start > entry.Start ? pause.Start : entry.Start;
                var end = pause.End < entry.End ? pause.End : entry.End;
                if (end > start)
                {
                    total += (int)(end - start).TotalMinutes;
                }
            }
        }

        return total;
    }
}
