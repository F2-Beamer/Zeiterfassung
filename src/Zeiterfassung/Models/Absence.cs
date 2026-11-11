namespace Zeiterfassung.Models;

public class Absence
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public AbsenceType Type { get; set; }

    /// <summary>1.0 für ganzen Tag, 0.5 für halben Tag.</summary>
    public decimal Portion { get; set; } = 1m;

    public string? Note { get; set; }

    /// <summary>
    /// Ob an demselben Tag trotzdem gearbeitet (gebucht oder pausiert) werden darf:
    /// Freizeitausgleich immer, jede halbe Abwesenheit (Urlaub oder Krank). Ganzer Urlaub und ganz krank schließen Arbeit aus.
    /// </summary>
    public static bool AllowsWork(AbsenceType type, decimal portion) =>
        type == AbsenceType.CompensatoryTime || portion == 0.5m;

    /// <summary>
    /// Die einzige Ausnahme von „eine Abwesenheit pro Tag“: halber Urlaub zusammen mit halbem Freizeitausgleich.
    /// </summary>
    public static bool IsHalfDayPair(AbsenceType first, decimal firstPortion, AbsenceType second, decimal secondPortion) =>
        firstPortion == 0.5m && secondPortion == 0.5m
        && ((first == AbsenceType.Vacation && second == AbsenceType.CompensatoryTime)
            || (first == AbsenceType.CompensatoryTime && second == AbsenceType.Vacation));
}

public enum AbsenceType
{
    Vacation = 1,
    Sick = 2,
    CompensatoryTime = 3
}
