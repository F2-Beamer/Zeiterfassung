namespace Zeiterfassung.Models;

public class Customer
{
    public int Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Archivierte Kunden erscheinen nicht mehr bei den Live-Buttons.</summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; }
}
