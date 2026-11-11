namespace Zeiterfassung.Models;

/// <summary>Pause ohne Kundenbezug. Ihre Dauer wird von der Ist-Zeit des Tages abgezogen.</summary>
public class PauseEntry
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }

    public int Minutes => (int)(End - Start).TotalMinutes;
}
