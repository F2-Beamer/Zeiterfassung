namespace Zeiterfassung.Models;

/// <summary>Überstunden-Startsaldo mit Stichtag. Es existiert genau ein Eintrag.</summary>
public class OvertimeOpening
{
    public int Id { get; set; }

    public DateOnly AsOfDate { get; set; }

    /// <summary>Saldo in Minuten, negativ oder positiv.</summary>
    public int Minutes { get; set; }
}
