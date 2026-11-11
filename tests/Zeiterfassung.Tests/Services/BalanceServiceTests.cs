using FluentAssertions;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;
using Zeiterfassung.Services;
using Zeiterfassung.Tests.Support;

namespace Zeiterfassung.Tests.Services;

public class BalanceServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.FromHours(1)));
    private readonly MasterDataService _masterData;
    private readonly BalanceService _balance;
    private readonly TimeTrackingService _tracking;
    private readonly AbsenceService _absences;
    private readonly SettingsService _settings;

    public BalanceServiceTests()
    {
        _masterData = new MasterDataService(_database.Factory);
        _settings = new SettingsService(_database.Factory);
        _balance = new BalanceService(_database.Factory, _settings, _time);
        _tracking = new TimeTrackingService(_database.Factory, _time);
        _absences = new AbsenceService(_database.Factory);

        _masterData.SaveProfile(new WorkTimeProfile
        {
            ValidFrom = new DateOnly(2026, 1, 1),
            WeeklyMinutes = 40 * 60
        });
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public void Ueberstunden_starten_beim_Startsaldo_und_zaehlen_nur_bis_heute()
    {
        _masterData.SaveOvertimeOpening(new DateOnly(2027, 1, 3), 60);

        // 2027-01-04 (Montag): 8 h Ist gegen 8 h Soll → Saldo 0 für den Tag
        var monday = new DateOnly(2027, 1, 4);
        _time.Now = new DateTimeOffset(2027, 1, 4, 18, 0, 0, TimeSpan.FromHours(1));
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = CreateCustomer(),
            Date = monday,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0),
            BreakMinutes = 0
        });

        _balance.GetOvertimeBalance(new DateOnly(2027, 1, 4)).Should().Be(60);
    }

    [Fact]
    public void Startsaldo_bleibt_unveraendert_wenn_Datum_vor_dem_Stichtag_liegt()
    {
        _masterData.SaveOvertimeOpening(new DateOnly(2026, 12, 31), 90);

        _balance.GetOvertimeBalance(new DateOnly(2026, 6, 1)).Should().Be(90);
    }

    [Fact]
    public void Urlaubsrest_uebernimmt_den_Rest_des_Vorjahres_automatisch()
    {
        _masterData.SaveEntitlement(2026, 30m, 0m, openingCarryOver: 5m);
        _masterData.SaveEntitlement(2027, 30m, 0m, openingCarryOver: null);

        // 2026: 5 + 30 = 35, davon 10 genommen → Rest 25
        _absences.AddAbsence(new DateOnly(2026, 7, 6), AbsenceType.Vacation, 1m, null);
        _absences.AddAbsence(new DateOnly(2026, 7, 7), AbsenceType.Vacation, 1m, null);
        _absences.AddAbsence(new DateOnly(2026, 7, 8), AbsenceType.Vacation, 1m, null);
        _absences.AddAbsence(new DateOnly(2026, 7, 9), AbsenceType.Vacation, 1m, null);
        _absences.AddAbsence(new DateOnly(2026, 7, 10), AbsenceType.Vacation, 1m, null);
        _absences.AddAbsence(new DateOnly(2026, 8, 3), AbsenceType.Vacation, 1m, null);
        _absences.AddAbsence(new DateOnly(2026, 8, 4), AbsenceType.Vacation, 1m, null);
        _absences.AddAbsence(new DateOnly(2026, 8, 5), AbsenceType.Vacation, 1m, null);
        _absences.AddAbsence(new DateOnly(2026, 8, 6), AbsenceType.Vacation, 1m, null);
        _absences.AddAbsence(new DateOnly(2026, 8, 7), AbsenceType.Vacation, 1m, null);

        var status2026 = _balance.GetVacation(2026);
        status2026.TakenDays.Should().Be(10m);
        status2026.RemainingDays.Should().Be(25m);

        var status2027 = _balance.GetVacation(2027);
        status2027.CarryOverDays.Should().Be(25m, "ohne Startwert übernimmt 2027 den Rest aus 2026");
    }

    [Fact]
    public void Ohne_Startwert_uebernimmt_das_Folgejahr_den_Rest_des_Vorjahres()
    {
        _masterData.SaveEntitlement(2026, 30m, 0m, openingCarryOver: 0m);
        _masterData.SaveEntitlement(2027, 30m, 0m, openingCarryOver: null);
        _absences.AddAbsence(new DateOnly(2026, 7, 6), AbsenceType.Vacation, 0.5m, null);

        _balance.GetVacation(2027).CarryOverDays.Should().Be(29.5m);
    }

    [Fact]
    public void Halber_Urlaubstag_zaehlt_als_halber_Tag()
    {
        _masterData.SaveEntitlement(2026, 30m, 0m, openingCarryOver: 0m);
        _absences.AddAbsence(new DateOnly(2026, 7, 6), AbsenceType.Vacation, 0.5m, null);

        _balance.GetVacation(2026).TakenDays.Should().Be(0.5m);
    }

    [Fact]
    public void Feiertag_im_Bundesland_setzt_Soll_auf_null()
    {
        _settings.SetFederalState("DE-BY");
        var holidayDate = new DateOnly(2026, 10, 3);
        using (var db = _database.Factory.CreateDbContext())
        {
            db.Holidays.Add(new Holiday
            {
                Date = holidayDate,
                Name = "Tag der Deutschen Einheit",
                State = "DE-BY",
                FetchedAt = DateTime.UtcNow
            });
            db.SaveChanges();
        }

        var day = _balance.GetDay(holidayDate);

        day.TargetMinutes.Should().Be(0);
        day.HolidayName.Should().Be("Tag der Deutschen Einheit");
    }

    private int CreateCustomer()
    {
        using var db = _database.Factory.CreateDbContext();
        var customer = new Customer { Name = "Test", CreatedAt = DateTime.UtcNow };
        db.Customers.Add(customer);
        db.SaveChanges();
        return customer.Id;
    }
}
