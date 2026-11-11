using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Data;
using Zeiterfassung.Models;

namespace Zeiterfassung.Services;

public sealed class SettingsService(IDbContextFactory<AppDbContext> factory)
{
    public const string FederalStateKey = "FederalState";
    public const string AutoBackupKey = "AutoBackupOnStart";
    public const string DefaultFederalState = "DE-BY";

    public static IReadOnlyList<(string Code, string Name)> FederalStates { get; } =
    [
        ("DE-BW", "Baden-Württemberg"),
        ("DE-BY", "Bayern"),
        ("DE-BE", "Berlin"),
        ("DE-BB", "Brandenburg"),
        ("DE-HB", "Bremen"),
        ("DE-HH", "Hamburg"),
        ("DE-HE", "Hessen"),
        ("DE-MV", "Mecklenburg-Vorpommern"),
        ("DE-NI", "Niedersachsen"),
        ("DE-NW", "Nordrhein-Westfalen"),
        ("DE-RP", "Rheinland-Pfalz"),
        ("DE-SL", "Saarland"),
        ("DE-SN", "Sachsen"),
        ("DE-ST", "Sachsen-Anhalt"),
        ("DE-SH", "Schleswig-Holstein"),
        ("DE-TH", "Thüringen")
    ];

    public string GetValue(string key, string defaultValue)
    {
        using var db = factory.CreateDbContext();
        return db.AppSettings.Where(s => s.Key == key).Select(s => s.Value).FirstOrDefault() ?? defaultValue;
    }

    public void SetValue(string key, string value)
    {
        using var db = factory.CreateDbContext();
        var setting = db.AppSettings.Find(key);
        if (setting is null)
        {
            db.AppSettings.Add(new AppSetting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        db.SaveChanges();
    }

    public string GetFederalState() => GetValue(FederalStateKey, DefaultFederalState);

    public void SetFederalState(string code) => SetValue(FederalStateKey, code);

    public bool IsAutoBackupEnabled() => GetValue(AutoBackupKey, "true") == "true";

    public void SetAutoBackupEnabled(bool enabled) => SetValue(AutoBackupKey, enabled ? "true" : "false");
}
