using System.Globalization;

namespace Zeiterfassung.ViewModels;

/// <summary>Formatierung und Eingabe-Parsing für Zeiten und Tage im deutschen Format.</summary>
public static class TimeFormat
{
    public static readonly CultureInfo German = new("de-DE");

    private static readonly string[] ClockFormats = ["H:mm", "HH:mm"];

    /// <summary>Minuten als H:mm, z. B. 165 → "2:45".</summary>
    public static string Hm(int minutes)
    {
        var sign = minutes < 0 ? "-" : string.Empty;
        var abs = Math.Abs(minutes);
        return $"{sign}{abs / 60}:{abs % 60:00}";
    }

    /// <summary>Saldo mit Vorzeichen, z. B. "+1:18" oder "-0:30".</summary>
    public static string Signed(int minutes) => minutes > 0 ? "+" + Hm(minutes) : Hm(minutes);

    public static string Days(decimal days) => days.ToString("0.##", German) + " Tage";

    public static bool TryParseClock(string? text, out TimeOnly value) =>
        TimeOnly.TryParseExact(text?.Trim(), ClockFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    /// <summary>Akzeptiert "H:mm" oder Dezimalstunden wie "7,5".</summary>
    public static bool TryParseHours(string? text, out int minutes)
    {
        minutes = 0;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        var negative = trimmed.StartsWith('-');
        var unsigned = trimmed.TrimStart('+', '-');

        if (unsigned.Contains(':'))
        {
            var parts = unsigned.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var h) || !int.TryParse(parts[1], out var m))
            {
                return false;
            }

            minutes = (h * 60 + m) * (negative ? -1 : 1);
            return true;
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Number, German, out var hours))
        {
            return false;
        }

        minutes = (int)Math.Round(hours * 60m, MidpointRounding.AwayFromZero);
        return true;
    }

    public static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text?.Trim(), NumberStyles.Number, German, out value);
}
