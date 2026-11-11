using FluentAssertions;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;
using Zeiterfassung.Services;
using Zeiterfassung.Tests.Support;

namespace Zeiterfassung.Tests.Services;

public class AbsenceConflictTests : IDisposable
{
    private static readonly DateOnly Day = new(2027, 1, 15);

    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.FromHours(1)));
    private readonly TimeTrackingService _tracking;
    private readonly AbsenceService _absences;
    private readonly CustomerService _customers;

    public AbsenceConflictTests()
    {
        _tracking = new TimeTrackingService(_database.Factory, _time);
        _absences = new AbsenceService(_database.Factory);
        _customers = new CustomerService(_database.Factory, _time);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public void Abwesenheit_an_einem_Tag_mit_Buchung_wird_abgelehnt()
    {
        var customerId = _customers.AddCustomer("Müller GmbH").Id;
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0)
        });

        var act = () => _absences.AddAbsence(Day, AbsenceType.Vacation, 1m, null);

        act.Should().Throw<DomainException>().WithMessage("*15.01.2027*");
        _absences.GetAbsences(Day, Day).Should().BeEmpty();
    }

    [Fact]
    public void Abwesenheit_an_einem_Tag_mit_nur_Pause_wird_abgelehnt()
    {
        _tracking.AddPause(Day, new TimeOnly(12, 0), new TimeOnly(12, 30));

        var act = () => _absences.AddAbsence(Day, AbsenceType.Vacation, 1m, null);

        act.Should().Throw<DomainException>();
        _absences.GetAbsences(Day, Day).Should().BeEmpty();
    }

    [Fact]
    public void Halber_Krankentag_zusammen_mit_Buchung_ist_erlaubt()
    {
        var customerId = _customers.AddCustomer("Müller GmbH").Id;
        _absences.AddAbsence(Day, AbsenceType.Sick, 0.5m, null);

        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(12, 0)
        });

        _tracking.GetEntries(Day).Should().ContainSingle();
        _absences.GetAbsences(Day, Day).Should().ContainSingle();
    }

    [Fact]
    public void Halber_Urlaub_zusammen_mit_halbem_Freizeitausgleich_ist_erlaubt()
    {
        _absences.AddAbsence(Day, AbsenceType.Vacation, 0.5m, null);

        _absences.AddAbsence(Day, AbsenceType.CompensatoryTime, 0.5m, null);

        _absences.GetAbsences(Day, Day).Should().HaveCount(2);
    }

    [Fact]
    public void Ganzer_Freizeitausgleich_neben_halbem_Urlaub_ist_nicht_erlaubt()
    {
        _absences.AddAbsence(Day, AbsenceType.Vacation, 0.5m, null);

        var act = () => _absences.AddAbsence(Day, AbsenceType.CompensatoryTime, 1m, null);

        act.Should().Throw<DomainException>();
        _absences.GetAbsences(Day, Day).Should().ContainSingle();
    }

    [Fact]
    public void Pro_Tag_ist_nur_eine_Abwesenheit_erlaubt_auch_bei_anderem_Typ()
    {
        _absences.AddAbsence(Day, AbsenceType.Sick, 0.5m, null);

        var act = () => _absences.AddAbsence(Day, AbsenceType.Vacation, 0.5m, null);

        act.Should().Throw<DomainException>().WithMessage("*pro Tag nur eine*");
        _absences.GetAbsences(Day, Day).Should().ContainSingle();
    }

    [Fact]
    public void Buchung_an_einem_Tag_mit_Abwesenheit_wird_abgelehnt()
    {
        _absences.AddAbsence(Day, AbsenceType.Vacation, 1m, null);
        var customerId = _customers.AddCustomer("Müller GmbH").Id;

        var act = () => _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0)
        });

        act.Should().Throw<DomainException>().WithMessage("*Abwesenheit*");
        _tracking.GetEntries(Day).Should().BeEmpty();
    }

    [Fact]
    public void Pause_an_einem_Tag_mit_Abwesenheit_wird_abgelehnt()
    {
        _absences.AddAbsence(Day, AbsenceType.Sick, 1m, null);

        var act = () => _tracking.AddPause(Day, new TimeOnly(12, 0), new TimeOnly(12, 30));

        act.Should().Throw<DomainException>().WithMessage("*Abwesenheit*");
        _tracking.GetPauses(Day).Should().BeEmpty();
    }

    [Fact]
    public void Halber_Urlaub_zusammen_mit_Buchung_ist_erlaubt()
    {
        var customerId = _customers.AddCustomer("Müller GmbH").Id;
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(12, 0)
        });

        _absences.AddAbsence(Day, AbsenceType.Vacation, 0.5m, null);

        _absences.GetAbsences(Day, Day).Should().ContainSingle();
    }

    [Fact]
    public void Freizeitausgleich_zusammen_mit_Buchung_ist_erlaubt_in_beiden_Reihenfolgen()
    {
        var customerId = _customers.AddCustomer("Müller GmbH").Id;
        _absences.AddAbsence(Day, AbsenceType.CompensatoryTime, 0.5m, null);

        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(10, 0)
        });

        _tracking.GetEntries(Day).Should().ContainSingle();
        _absences.GetAbsences(Day, Day).Should().ContainSingle();

        var otherDay = Day.AddDays(1);
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = otherDay,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(10, 0)
        });
        _absences.AddAbsence(otherDay, AbsenceType.CompensatoryTime, 1m, null);

        _absences.GetAbsences(otherDay, otherDay).Should().ContainSingle();
    }

    [Fact]
    public void Ganzer_Urlaub_zusammen_mit_Pause_bleibt_gesperrt()
    {
        _tracking.AddPause(Day, new TimeOnly(12, 0), new TimeOnly(12, 30));

        var act = () => _absences.AddAbsence(Day, AbsenceType.Vacation, 1m, null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Abwesenheit_an_leerem_Tag_wird_weiterhin_gespeichert()
    {
        var customerId = _customers.AddCustomer("Müller GmbH").Id;
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0)
        });

        _absences.AddAbsence(Day.AddDays(1), AbsenceType.Vacation, 1m, null);

        _absences.GetAbsences(Day.AddDays(1), Day.AddDays(1)).Should().ContainSingle();
    }
}
