namespace Zeiterfassung.Models;

/// <summary>Laufender Live-Timer. Es existiert höchstens eine Zeile (Id = 1).</summary>
public class ActiveTimer
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;

    public int CustomerId { get; set; }

    public DateTime StartedAt { get; set; }

    /// <summary>Gesetzt, solange der Timer pausiert ist.</summary>
    public DateTime? PausedAt { get; set; }

    public int AccumulatedBreakSeconds { get; set; }
}
