using Zeiterfassung.Models;

namespace Zeiterfassung.Domain;

/// <summary>Auswertung eines Kalendertags.</summary>
/// <param name="Date">Datum des Tages.</param>
/// <param name="Entries">Zeitbuchungen des Tages.</param>
/// <param name="Absences">Abwesenheiten des Tages.</param>
/// <param name="HolidayName">Name des Feiertags, sonst null.</param>
/// <param name="ActualMinutes">Netto-Ist-Zeit in Minuten.</param>
/// <param name="TargetMinutes">Soll-Zeit nach Abzug von Feiertag und Urlaub/Krank.</param>
/// <param name="VacationDaysUsed">Anteil an Urlaubstagen (0,5 oder 1), der an diesem Tag zählt.</param>
public sealed record DayResult(
    DateOnly Date,
    IReadOnlyList<TimeEntry> Entries,
    IReadOnlyList<Absence> Absences,
    string? HolidayName,
    int ActualMinutes,
    int TargetMinutes,
    decimal VacationDaysUsed)
{
    /// <summary>Saldo des Tages in Minuten (Ist minus Soll).</summary>
    public int BalanceMinutes => ActualMinutes - TargetMinutes;
}
