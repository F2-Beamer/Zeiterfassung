using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Data;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;

namespace Zeiterfassung.Services;

/// <summary>Stammdaten: Soll-Arbeitszeit-Profile, Urlaubsanspruch und Überstunden-Startsaldo.</summary>
public sealed class MasterDataService(IDbContextFactory<AppDbContext> factory)
{
    public IReadOnlyList<WorkTimeProfile> GetProfiles()
    {
        using var db = factory.CreateDbContext();
        return db.WorkTimeProfiles
            .Include(p => p.Overrides)
            .OrderBy(p => p.ValidFrom)
            .ToList();
    }

    /// <summary>Legt ein Profil an oder aktualisiert es (Id &gt; 0). Overrides werden vollständig ersetzt.</summary>
    public void SaveProfile(WorkTimeProfile profile)
    {
        if (profile.WeeklyMinutes is <= 0 or > 7 * 24 * 60)
        {
            throw new DomainException("Die Wochenstunden müssen zwischen 0 und 168 liegen.");
        }

        if (profile.Overrides.Any(o => o.Minutes < 0))
        {
            throw new DomainException("Die Tagesstunden dürfen nicht negativ sein.");
        }

        if (profile.Overrides.GroupBy(o => o.Day).Any(g => g.Count() > 1))
        {
            throw new DomainException("Ein Wochentag darf nur einmal überschrieben werden.");
        }

        using var db = factory.CreateDbContext();

        var duplicateStart = db.WorkTimeProfiles.Any(p => p.ValidFrom == profile.ValidFrom && p.Id != profile.Id);
        if (duplicateStart)
        {
            throw new DomainException("Für dieses Gültigkeitsdatum existiert bereits ein Profil.");
        }

        var overrides = profile.Overrides
            .Select(o => new WorkTimeDayOverride { Day = o.Day, Minutes = o.Minutes })
            .ToList();

        if (profile.Id == 0)
        {
            db.WorkTimeProfiles.Add(new WorkTimeProfile
            {
                ValidFrom = profile.ValidFrom,
                WeeklyMinutes = profile.WeeklyMinutes,
                Overrides = overrides
            });
        }
        else
        {
            var existing = db.WorkTimeProfiles
                .Include(p => p.Overrides)
                .Single(p => p.Id == profile.Id);

            existing.ValidFrom = profile.ValidFrom;
            existing.WeeklyMinutes = profile.WeeklyMinutes;
            db.WorkTimeDayOverrides.RemoveRange(existing.Overrides);
            existing.Overrides = overrides;
        }

        db.SaveChanges();
    }

    public void DeleteProfile(int id)
    {
        using var db = factory.CreateDbContext();
        var profile = db.WorkTimeProfiles.Find(id);
        if (profile is not null)
        {
            db.WorkTimeProfiles.Remove(profile);
            db.SaveChanges();
        }
    }

    public VacationEntitlement? GetEntitlement(int year)
    {
        using var db = factory.CreateDbContext();
        return db.VacationEntitlements.FirstOrDefault(v => v.Year == year);
    }

    public void SaveEntitlement(int year, decimal days, decimal correctionDays, decimal? openingCarryOver)
    {
        if (days < 0)
        {
            throw new DomainException("Der Urlaubsanspruch darf nicht negativ sein.");
        }

        using var db = factory.CreateDbContext();
        var entitlement = db.VacationEntitlements.FirstOrDefault(v => v.Year == year);
        if (entitlement is null)
        {
            db.VacationEntitlements.Add(new VacationEntitlement
            {
                Year = year,
                Days = days,
                CorrectionDays = correctionDays,
                OpeningCarryOverDays = openingCarryOver
            });
        }
        else
        {
            entitlement.Days = days;
            entitlement.CorrectionDays = correctionDays;
            entitlement.OpeningCarryOverDays = openingCarryOver;
        }

        db.SaveChanges();
    }

    public OvertimeOpening? GetOvertimeOpening()
    {
        using var db = factory.CreateDbContext();
        return db.OvertimeOpenings.OrderBy(o => o.Id).FirstOrDefault();
    }

    /// <summary>Speichert den Überstunden-Startsaldo (genau ein Eintrag).</summary>
    public void SaveOvertimeOpening(DateOnly asOfDate, int minutes)
    {
        using var db = factory.CreateDbContext();
        var opening = db.OvertimeOpenings.OrderBy(o => o.Id).FirstOrDefault();
        if (opening is null)
        {
            db.OvertimeOpenings.Add(new OvertimeOpening { AsOfDate = asOfDate, Minutes = minutes });
        }
        else
        {
            opening.AsOfDate = asOfDate;
            opening.Minutes = minutes;
        }

        db.SaveChanges();
    }
}
