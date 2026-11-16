namespace Zeiterfassung.Models;

/// <summary>Pause ohne Kundenbezug. Soweit sie in eine Buchung fällt, wird sie von deren Zeit abgezogen.</summary>
public class PauseEntry
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }

    public int Minutes => (int)(End - Start).TotalMinutes;
}
