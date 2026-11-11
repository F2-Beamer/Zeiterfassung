namespace Zeiterfassung.Models;

public class TimeEntry
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public DateOnly Date { get; set; }

    public TimeOnly Start { get; set; }

    public TimeOnly End { get; set; }

    public int BreakMinutes { get; set; }

    public string? Note { get; set; }

    /// <summary>True, wenn die Buchung über den Live-Timer entstanden ist.</summary>
    public bool FromTimer { get; set; }

    /// <summary>Netto-Arbeitszeit in Minuten (Brutto minus Pausen).</summary>
    public int NetMinutes => (int)(End - Start).TotalMinutes - BreakMinutes;
}
