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
        // Pausen ohne Kundenbezug werden von der Netto-Zeit der Buchungen abgezogen.
        var pauseMinutes = pauses?.Sum(p => p.Minutes) ?? 0;
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
}
