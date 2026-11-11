namespace Zeiterfassung.Models;

/// <summary>Feste Soll-Minuten für einen Wochentag. Die übrigen Wochenstunden verteilen sich auf die Arbeitstage ohne Override.</summary>
public class WorkTimeDayOverride
{
    public int Id { get; set; }

    public int ProfileId { get; set; }

    /// <summary>0 = Sonntag bis 6 = Samstag, wie <see cref="DayOfWeek"/>.</summary>
    public DayOfWeek Day { get; set; }

    public int Minutes { get; set; }
}
