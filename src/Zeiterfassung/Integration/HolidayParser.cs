using System.Text.Json;
using Zeiterfassung.Models;

namespace Zeiterfassung.Integration;

/// <summary>
/// Liest die Antwort von date.nager.at (GET /api/v3/PublicHolidays/{year}/DE).
/// Gesetzliche Feiertage ohne Länderangabe gelten bundesweit, sonst nur für die angegebenen Länder.
/// </summary>
public static class HolidayParser
{
    public static IReadOnlyList<Holiday> Parse(string json, string state, DateTime fetchedAt)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<Holiday>();

        foreach (var item in document.RootElement.EnumerateArray())
        {
            var counties = item.TryGetProperty("counties", out var countiesElement) && countiesElement.ValueKind == JsonValueKind.Array
                ? countiesElement.EnumerateArray().Select(c => c.GetString()).ToList()
                : null;

            var appliesToState = counties is null || counties.Contains(state);
            if (!appliesToState)
            {
                continue;
            }

            var date = DateOnly.ParseExact(item.GetProperty("date").GetString()!, "yyyy-MM-dd");
            var name = item.TryGetProperty("localName", out var localName) ? localName.GetString() : null;

            result.Add(new Holiday
            {
                Date = date,
                Name = name ?? "Feiertag",
                State = state,
                FetchedAt = fetchedAt
            });
        }

        return result;
    }
}
