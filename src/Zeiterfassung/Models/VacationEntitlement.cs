namespace Zeiterfassung.Models;

public class VacationEntitlement
{
    public int Id { get; set; }

    public int Year { get; set; }

    /// <summary>Jahresanspruch in Tagen.</summary>
    public decimal Days { get; set; }

    /// <summary>Optionale manuelle Korrektur in Tagen (positiv oder negativ).</summary>
    public decimal CorrectionDays { get; set; }

    /// <summary>
    /// Manueller Startwert für den Übertrag. Wird nur genutzt, wenn für das Jahr kein
    /// automatischer Übertrag aus dem Vorjahr berechnet werden kann (Einstieg).
    /// </summary>
    public decimal? OpeningCarryOverDays { get; set; }
}
