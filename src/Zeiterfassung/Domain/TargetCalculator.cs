using Zeiterfassung.Models;

namespace Zeiterfassung.Domain;

/// <summary>
/// Berechnet die Soll-Minuten eines Wochentags aus einem Wochenstunden-Profil.
/// Wochenstunden werden gleichmäßig auf die Mo–Fr-Arbeitstage verteilt, die keinen festen Override haben.
/// Feste Overrides zählen zur Wochensumme mit.
/// </summary>
public static class TargetCalculator
{
    private static readonly DayOfWeek[] DefaultWorkdays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    ];

    public static int BaseTargetMinutes(WorkTimeProfile? profile, DayOfWeek day)
    {
        if (profile is null)
        {
            return 0;
        }

        var overrides = profile.Overrides.ToDictionary(o => o.Day, o => o.Minutes);
        if (overrides.TryGetValue(day, out var fixedMinutes))
        {
            return fixedMinutes;
        }

        if (!DefaultWorkdays.Contains(day))
        {
            return 0;
        }

        var freeWorkdays = DefaultWorkdays.Count(d => !overrides.ContainsKey(d));
        if (freeWorkdays == 0)
        {
            return 0;
        }

        var remaining = Math.Max(0, profile.WeeklyMinutes - overrides.Values.Sum());
        return (int)Math.Round(remaining / (double)freeWorkdays, MidpointRounding.AwayFromZero);
    }
}
