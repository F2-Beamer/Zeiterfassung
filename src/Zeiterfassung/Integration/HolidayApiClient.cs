using System.Net.Http;
namespace Zeiterfassung.Integration;

/// <summary>Ruft die Feiertage eines Jahres über date.nager.at ab (kein API-Schlüssel nötig).</summary>
public sealed class HolidayApiClient(HttpClient httpClient)
{
    public const string BaseUrl = "https://date.nager.at/";

    public async Task<string> GetPublicHolidaysJsonAsync(int year, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"api/v3/PublicHolidays/{year}/DE", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
