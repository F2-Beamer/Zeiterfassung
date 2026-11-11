namespace Zeiterfassung.Models;

/// <summary>
/// Soll-Arbeitszeit als Wochenstunden. Gilt ab <see cref="ValidFrom"/> bis zum nächsten Profil.
/// </summary>
public class WorkTimeProfile
{
    public int Id { get; set; }

    public DateOnly ValidFrom { get; set; }

    public int WeeklyMinutes { get; set; }

    public List<WorkTimeDayOverride> Overrides { get; set; } = [];
}
