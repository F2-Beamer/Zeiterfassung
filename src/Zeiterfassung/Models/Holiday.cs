namespace Zeiterfassung.Models;

/// <summary>Gecachter Feiertag für ein Bundesland, abgerufen über date.nager.at.</summary>
public class Holiday
{
    public int Id { get; set; }

    public DateOnly Date { get; set; }

    public required string Name { get; set; }

    /// <summary>ISO-Code des Bundeslands, z. B. "DE-BY".</summary>
    public required string State { get; set; }

    public DateTime FetchedAt { get; set; }
}
