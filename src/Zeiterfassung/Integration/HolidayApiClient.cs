using System.IO;
using System.Net.Http;
namespace Zeiterfassung.Integration;

/// <summary>Ruft die Feiertage eines Jahres über date.nager.at ab (kein API-Schlüssel nötig).</summary>
public sealed class HolidayApiClient(HttpClient httpClient)
{
    public const string BaseUrl = "https://date.nager.at/";

    /// <summary>Obergrenze für die Antwort. Eine Jahresliste ist wenige Kilobyte groß.</summary>
    public const long MaxResponseBytes = 1024 * 1024;

    public async Task<string> GetPublicHolidaysJsonAsync(int year, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"api/v3/PublicHolidays/{year}/DE",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is { } declared && declared > MaxResponseBytes)
        {
            throw new HttpRequestException("Die Antwort des Feiertagsdienstes ist zu groß.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxResponseBytes)
            {
                throw new HttpRequestException("Die Antwort des Feiertagsdienstes ist zu groß.");
            }

            buffer.Write(chunk, 0, read);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
