using FluentAssertions;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;

namespace Zeiterfassung.Tests.Domain;

public class TargetAndDayCalculatorTests
{
    private static WorkTimeProfile Profile(int weeklyHours, params (DayOfWeek day, int minutes)[] overrides) => new()
    {
        ValidFrom = new DateOnly(2026, 1, 1),
        WeeklyMinutes = weeklyHours * 60,
        Overrides = overrides.Select(o => new WorkTimeDayOverride { Day = o.day, Minutes = o.minutes }).ToList()
    };

    [Fact]
    public void Vollzeit_40h_verteilt_sich_auf_8h_Mo_bis_Fr()
    {
        var profile = Profile(40);

        TargetCalculator.BaseTargetMinutes(profile, DayOfWeek.Monday).Should().Be(480);
        TargetCalculator.BaseTargetMinutes(profile, DayOfWeek.Friday).Should().Be(480);
        TargetCalculator.BaseTargetMinutes(profile, DayOfWeek.Saturday).Should().Be(0);
    }

    [Fact]
    public void Fester_Override_am_Freitag_verteilt_den_Rest_auf_die_anderen_Tage()
    {
        var profile = Profile(40, (DayOfWeek.Friday, 360));

        TargetCalculator.BaseTargetMinutes(profile, DayOfWeek.Friday).Should().Be(360);
        TargetCalculator.BaseTargetMinutes(profile, DayOfWeek.Monday).Should().Be(510);
    }

    [Fact]
    public void Ohne_Profil_ist_das_Soll_null()
    {
        TargetCalculator.BaseTargetMinutes(null, DayOfWeek.Monday).Should().Be(0);
    }

    [Fact]
    public void Feiertag_setzt_das_Soll_auf_null()
    {
        var day = DayCalculator.Calculate(
            new DateOnly(2026, 10, 3), Profile(40), "Tag der Deutschen Einheit", [], []);

        day.TargetMinutes.Should().Be(0);
    }

    [Fact]
    public void Ganztags_Urlaub_entfernt_das_Soll_und_zaehlt_einen_Urlaubstag()
    {
        var absence = new Absence { Type = AbsenceType.Vacation, Portion = 1m };

        var day = DayCalculator.Calculate(new DateOnly(2026, 10, 28), Profile(40), null, [absence], []);

        day.TargetMinutes.Should().Be(0);
        day.VacationDaysUsed.Should().Be(1m);
    }

    [Fact]
    public void Halber_Urlaub_halbiert_das_Soll_und_zaehlt_einen_halben_Tag()
    {
        var absence = new Absence { Type = AbsenceType.Vacation, Portion = 0.5m };

        var day = DayCalculator.Calculate(new DateOnly(2026, 10, 28), Profile(40), null, [absence], []);

        day.TargetMinutes.Should().Be(240);
        day.VacationDaysUsed.Should().Be(0.5m);
    }

    [Fact]
    public void Krankheit_entfernt_das_Soll_ohne_Urlaubstag()
    {
        var absence = new Absence { Type = AbsenceType.Sick, Portion = 1m };

        var day = DayCalculator.Calculate(new DateOnly(2026, 10, 28), Profile(40), null, [absence], []);

        day.TargetMinutes.Should().Be(0);
        day.VacationDaysUsed.Should().Be(0m);
    }

    [Fact]
    public void Freizeitausgleich_laesst_das_Soll_stehen_und_der_Saldo_faellt_um_das_Soll()
    {
        var absence = new Absence { Type = AbsenceType.CompensatoryTime, Portion = 1m };

        var day = DayCalculator.Calculate(new DateOnly(2026, 10, 28), Profile(40), null, [absence], []);

        day.TargetMinutes.Should().Be(480);
        day.BalanceMinutes.Should().Be(-480);
    }

    [Fact]
    public void Urlaub_an_einem_Wochenende_zaehlt_nicht_als_Urlaubstag()
    {
        var absence = new Absence { Type = AbsenceType.Vacation, Portion = 1m };

        var day = DayCalculator.Calculate(new DateOnly(2026, 10, 31), Profile(40), null, [absence], []);

        day.VacationDaysUsed.Should().Be(0m);
    }
}
